using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Memory;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Providers;
using OpenAnima.Tests.TestHelpers;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests verifying the LLMModule sedimentation wiring (Phase 54, Plan 02).
/// Tests cover: backward compatibility when service is null, fire-and-forget trigger after response,
/// CancellationToken.None enforcement, and exception isolation from the main pipeline.
/// Uses fakes — no mocking libraries required.
/// </summary>
public class LLMModuleSedimentationTests : IDisposable
{
    private const string TestAnimaId = "anima-sed-wiring";

    private readonly string _tempRoot;
    private readonly LLMProviderRegistryService _registryService;

    public LLMModuleSedimentationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"llmmodule-sed-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _registryService = new LLMProviderRegistryService(
            _tempRoot, NullLogger<LLMProviderRegistryService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an LLMModule wired with a canned LLM service that returns a fixed response.
    /// Optional sedimentationService parameter allows test-specific injection.
    /// </summary>
    private LLMModule CreateModule(
        FakeSedimentationService? sedimentationService,
        string llmResponse = "canned-response")
    {
        var llmService = new SedimentationTestLLMService(llmResponse);
        var config = new NullAnimaModuleConfigService();
        var context = new SedimentationFakeModuleContext(TestAnimaId);
        var eventBus = new SedimentationNoOpEventBus();

        return new LLMModule(
            llmService: llmService,
            eventBus: eventBus,
            logger: NullLogger<LLMModule>.Instance,
            configService: config,
            animaContext: context,
            providerRegistry: NullLLMProviderRegistry.Instance,
            registryService: _registryService,
            router: null,
            memoryRecallService: null,
            stepRecorder: null,
            workspaceToolModule: null,
            sedimentationService: sedimentationService);
    }

    /// <summary>
    /// Invokes ExecuteWithMessagesListAsync directly via reflection, mirroring LLMModuleMemoryTests pattern.
    /// </summary>
    private static async Task InvokeAsync(LLMModule module, string userText = "hello")
    {
        var messages = new List<ChatMessageInput> { new("user", userText) };
        var method = typeof(LLMModule).GetMethod(
            "ExecuteWithMessagesListAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(module, [messages, CancellationToken.None])!;
        await task;
    }

    private static async Task WaitForSedimentationCallAsync(FakeSedimentationService service, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (service.Calls.Count > 0)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Sedimentation call did not arrive within {timeout.TotalMilliseconds}ms.");
    }

    // ── Test 1: backward compatibility when service is null ───────────────────

    [Fact]
    public async Task SedimentationService_IsNull_CompletesWithoutError()
    {
        // Arrange: no ISedimentationService injected
        var module = CreateModule(sedimentationService: null);

        // Act: must not throw
        var exception = await Record.ExceptionAsync(() => InvokeAsync(module));

        // Assert: LLMModule completes normally with null sedimentation service
        Assert.Null(exception);
        Assert.Equal(ModuleExecutionState.Completed, module.GetState());
    }

    // ── Test 2: SedimentAsync called after response publication ───────────────

    [Fact]
    public async Task SedimentationService_CalledAfterResponse()
    {
        // Arrange
        const string expectedResponse = "canned-sedimentation-response";
        var fakeSedimentation = new FakeSedimentationService();
        var module = CreateModule(fakeSedimentation, llmResponse: expectedResponse);

        // Act
        await InvokeAsync(module);

        await WaitForSedimentationCallAsync(fakeSedimentation, TimeSpan.FromSeconds(2));

        // Assert: SedimentAsync was called exactly once
        var call = Assert.Single(fakeSedimentation.Calls);
        Assert.Equal(TestAnimaId, call.AnimaId);
        Assert.Equal(expectedResponse, call.Response);

        // Messages snapshot should contain the original user message
        Assert.NotNull(call.Messages);
        Assert.Contains(call.Messages, m => m.Role == "user" && m.Content == "hello");
    }

    // ── Test 3: CancellationToken.None is used, not caller's token ───────────

    [Fact]
    public async Task SedimentationService_ReceivesCancellationTokenNone()
    {
        // Arrange
        var fakeSedimentation = new FakeSedimentationService();
        var module = CreateModule(fakeSedimentation);

        // Act
        await InvokeAsync(module);

        await WaitForSedimentationCallAsync(fakeSedimentation, TimeSpan.FromSeconds(2));

        // Assert: the token received by SedimentAsync is CancellationToken.None
        var call = Assert.Single(fakeSedimentation.Calls);
        Assert.Equal(CancellationToken.None, call.CancellationToken);
    }

    // ── Test 4: Exception in SedimentAsync does not propagate to caller ───────

    [Fact]
    public async Task SedimentationService_ThrowingDoesNotPropagateToLLMModule()
    {
        // Arrange: configure FakeSedimentationService to throw
        var fakeSedimentation = new FakeSedimentationService
        {
            ThrowOnCall = new InvalidOperationException("sedimentation exploded")
        };
        var module = CreateModule(fakeSedimentation);

        // Act: must not throw even though SedimentAsync will throw in background
        var exception = await Record.ExceptionAsync(() => InvokeAsync(module));

        // Assert: no exception propagated to the LLMModule caller
        Assert.Null(exception);

        // Assert: LLMModule state is Completed (not Error)
        Assert.Equal(ModuleExecutionState.Completed, module.GetState());
    }
}

// ── FakeSedimentationService ───────────────────────────────────────────────────

/// <summary>
/// Configurable fake for <see cref="ISedimentationService"/>.
/// Records all calls to SedimentAsync, including the CancellationToken received.
/// Can be configured to throw an exception on call to test error isolation.
/// </summary>
internal class FakeSedimentationService : ISedimentationService
{
    public record SedimentCall(
        string AnimaId,
        IReadOnlyList<ChatMessageInput> Messages,
        string Response,
        string? SourceStepId,
        CancellationToken CancellationToken);

    public List<SedimentCall> Calls { get; } = new();

    /// <summary>When set, SedimentAsync throws this exception.</summary>
    public Exception? ThrowOnCall { get; set; }

    public Task SedimentAsync(
        string animaId,
        IReadOnlyList<ChatMessageInput> messages,
        string llmResponse,
        string? sourceStepId,
        CancellationToken ct = default)
    {
        if (ThrowOnCall != null)
            throw ThrowOnCall;

        Calls.Add(new SedimentCall(animaId, messages, llmResponse, sourceStepId, ct));
        return Task.CompletedTask;
    }
}

// ── Supporting fakes ──────────────────────────────────────────────────────────

/// <summary>
/// Minimal ILLMService that returns a fixed success response for sedimentation wiring tests.
/// </summary>
internal class SedimentationTestLLMService : ILLMService
{
    private readonly string _response;

    public SedimentationTestLLMService(string response) => _response = response;

    public Task<LLMResult> CompleteAsync(
        IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        => Task.FromResult(new LLMResult(true, _response, null));

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessageInput> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(
        IReadOnlyList<ChatMessageInput> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

/// <summary>
/// Minimal IModuleContext returning a fixed AnimaId.
/// </summary>
internal class SedimentationFakeModuleContext : IModuleContext
{
    public SedimentationFakeModuleContext(string animaId) => ActiveAnimaId = animaId;
    public string ActiveAnimaId { get; }
    public event Action? ActiveAnimaChanged { add { } remove { } }
}

/// <summary>
/// Minimal IEventBus that discards all events and returns a no-op subscription.
/// </summary>
internal class SedimentationNoOpEventBus : IEventBus
{
    public Task PublishAsync<T>(ModuleEvent<T> evt, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default)
        => Task.FromResult<TResponse>(default!);

    public IDisposable Subscribe<TPayload>(
        string eventName,
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null)
        => new SedimentationNoOpDisposable();

    public IDisposable Subscribe<TPayload>(
        Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
        Func<ModuleEvent<TPayload>, bool>? filter = null)
        => new SedimentationNoOpDisposable();

    private class SedimentationNoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
