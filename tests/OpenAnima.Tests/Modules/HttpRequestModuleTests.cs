using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Events;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Services;
using OpenAnima.Tests.TestHelpers;

namespace OpenAnima.Tests.Modules;

/// <summary>
/// Integration tests for HttpRequestModule HTTP pipeline.
/// Covers: HTTP success/error routing, SSRF blocking, timeout, connection failure,
/// empty URL validation, and body port buffering.
/// </summary>
[Trait("Category", "HttpRequest")]
public class HttpRequestModuleTests
{
    // ─── Inner helpers ───────────────────────────────────────────────────────

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }

    /// <summary>
    /// Config service that returns a fixed dictionary for a given module.
    /// </summary>
    private sealed class TestConfigService : IAnimaModuleConfigService
    {
        private readonly Dictionary<string, string> _config;

        public TestConfigService(Dictionary<string, string> config)
        {
            _config = config;
        }

        public Dictionary<string, string> GetConfig(string animaId, string moduleId) => _config;

        public Task SetConfigAsync(string animaId, string moduleId, Dictionary<string, string> config)
            => Task.CompletedTask;

        public Task InitializeAsync() => Task.CompletedTask;
    }

    private static EventBus CreateEventBus()
        => new(NullLogger<EventBus>.Instance);

    private static IHttpClientFactory CreateHttpClientFactory(FakeHttpMessageHandler fakeHandler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("HttpRequest")
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }

    private record ModuleFixture(
        HttpRequestModule Module,
        EventBus EventBus,
        IAnimaModuleConfigService ConfigService);

    private static async Task<ModuleFixture> CreateModuleAsync(
        IHttpClientFactory factory,
        Dictionary<string, string>? config = null)
    {
        var eventBus = CreateEventBus();
        var configService = config != null
            ? (IAnimaModuleConfigService)new TestConfigService(config)
            : NullAnimaModuleConfigService.Instance;
        var animaContext = new AnimaContext();
        animaContext.SetActive("test-anima-id");

        var module = new HttpRequestModule(
            eventBus,
            factory,
            configService,
            animaContext,
            NullLogger<HttpRequestModule>.Instance);

        await module.InitializeAsync();

        return new ModuleFixture(module, eventBus, configService);
    }

    // ─── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuccessfulGet_PublishesBodyAndStatusCode_ErrorPortNotTriggered()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("hello world")
            }));

        var factory = CreateHttpClientFactory(fakeHandler);
        var fixture = await CreateModuleAsync(factory, new Dictionary<string, string>
        {
            ["url"] = "http://example.com/api",
            ["method"] = "GET",
            ["headers"] = "",
            ["body"] = ""
        });

        var bodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.body",
            (evt, ct) => { bodyTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.statusCode",
            (evt, ct) => { statusTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.error",
            (evt, ct) => { errorTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });

        // Act
        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.trigger",
            Payload = ""
        });

        // Assert
        var bodyCompleted = await Task.WhenAny(bodyTcs.Task, Task.Delay(5000));
        Assert.True(bodyCompleted == bodyTcs.Task, "Expected body port to fire within 5s");
        Assert.Equal("hello world", await bodyTcs.Task);

        var statusCompleted = await Task.WhenAny(statusTcs.Task, Task.Delay(5000));
        Assert.True(statusCompleted == statusTcs.Task, "Expected statusCode port to fire within 5s");
        Assert.Equal("200", await statusTcs.Task);

        // Error port must NOT have fired
        var errorCompleted = await Task.WhenAny(errorTcs.Task, Task.Delay(200));
        Assert.False(errorCompleted == errorTcs.Task, "Error port must NOT fire on success");
    }

    [Fact]
    public async Task Http404_PublishesBodyAndStatusCode404_ErrorPortNotTriggered()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found body")
            }));

        var factory = CreateHttpClientFactory(fakeHandler);
        var fixture = await CreateModuleAsync(factory, new Dictionary<string, string>
        {
            ["url"] = "http://example.com/missing",
            ["method"] = "GET",
            ["headers"] = "",
            ["body"] = ""
        });

        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.statusCode",
            (evt, ct) => { statusTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.error",
            (evt, ct) => { errorTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });

        // Act
        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.trigger",
            Payload = ""
        });

        // Assert
        var statusCompleted = await Task.WhenAny(statusTcs.Task, Task.Delay(5000));
        Assert.True(statusCompleted == statusTcs.Task, "Expected statusCode port to fire on 404");
        Assert.Equal("404", await statusTcs.Task);

        // Downstream decides what to do with 404 — error port must NOT fire
        var errorCompleted = await Task.WhenAny(errorTcs.Task, Task.Delay(200));
        Assert.False(errorCompleted == errorTcs.Task, "Error port must NOT fire on HTTP 404");
    }

    [Fact]
    public async Task Http500_PublishesBodyAndStatusCode500_ErrorPortNotTriggered()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error body")
            }));

        var factory = CreateHttpClientFactory(fakeHandler);
        var fixture = await CreateModuleAsync(factory, new Dictionary<string, string>
        {
            ["url"] = "http://example.com/unstable",
            ["method"] = "GET",
            ["headers"] = "",
            ["body"] = ""
        });

        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.statusCode",
            (evt, ct) => { statusTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.error",
            (evt, ct) => { errorTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });

        // Act
        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.trigger",
            Payload = ""
        });

        // Assert
        var statusCompleted = await Task.WhenAny(statusTcs.Task, Task.Delay(5000));
        Assert.True(statusCompleted == statusTcs.Task, "Expected statusCode port to fire on 500");
        Assert.Equal("500", await statusTcs.Task);

        // HTTP 500 is still a valid response — error port must NOT fire
        var errorCompleted = await Task.WhenAny(errorTcs.Task, Task.Delay(200));
        Assert.False(errorCompleted == errorTcs.Task, "Error port must NOT fire on HTTP 500");
    }

    [Fact]
    public async Task SsrfBlockedUrl_PublishesToErrorPort_BodyAndStatusCodeNotTriggered()
    {
        // Arrange — no fake handler needed, SSRF guard blocks before network call
        var factory = CreateHttpClientFactory(new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK))));

        var fixture = await CreateModuleAsync(factory, new Dictionary<string, string>
        {
            ["url"] = "http://10.0.0.1/api",  // private IP — SSRF blocked
            ["method"] = "GET",
            ["headers"] = "",
            ["body"] = ""
        });

        var errorTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.error",
            (evt, ct) => { errorTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.body",
            (evt, ct) => { bodyTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.statusCode",
            (evt, ct) => { statusTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });

        // Act
        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.trigger",
            Payload = ""
        });

        // Assert
        var errorCompleted = await Task.WhenAny(errorTcs.Task, Task.Delay(5000));
        Assert.True(errorCompleted == errorTcs.Task, "Expected error port to fire on SSRF-blocked URL");
        Assert.Contains("SsrfBlocked", await errorTcs.Task);

        // body and statusCode must NOT fire
        var bodyCompleted = await Task.WhenAny(bodyTcs.Task, Task.Delay(200));
        Assert.False(bodyCompleted == bodyTcs.Task, "body port must NOT fire when SSRF blocked");

        var statusCompleted = await Task.WhenAny(statusTcs.Task, Task.Delay(200));
        Assert.False(statusCompleted == statusTcs.Task, "statusCode port must NOT fire when SSRF blocked");
    }

    [Fact]
    public async Task Timeout_PublishesToErrorPort_BodyAndStatusCodeNotTriggered()
    {
        // Arrange — handler blocks until cancellation (simulates infinite hang)
        var fakeHandler = new FakeHttpMessageHandler(async (req, ct) =>
        {
            // Block indefinitely until token cancels (module's 10s timeout will trigger this)
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        var factory = CreateHttpClientFactory(fakeHandler);
        var fixture = await CreateModuleAsync(factory, new Dictionary<string, string>
        {
            ["url"] = "http://example.com/slow",
            ["method"] = "GET",
            ["headers"] = "",
            ["body"] = ""
        });

        var errorTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.error",
            (evt, ct) => { errorTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.body",
            (evt, ct) => { bodyTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.statusCode",
            (evt, ct) => { statusTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });

        // Act
        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.trigger",
            Payload = ""
        });

        // Assert — module has 10s timeout; test waits 15s to ensure it fires
        var errorCompleted = await Task.WhenAny(errorTcs.Task, Task.Delay(15000));
        Assert.True(errorCompleted == errorTcs.Task, "Expected error port to fire on timeout");
        Assert.Contains("Timeout", await errorTcs.Task);

        // body and statusCode must NOT fire
        var bodyCompleted = await Task.WhenAny(bodyTcs.Task, Task.Delay(200));
        Assert.False(bodyCompleted == bodyTcs.Task, "body port must NOT fire on timeout");

        var statusCompleted = await Task.WhenAny(statusTcs.Task, Task.Delay(200));
        Assert.False(statusCompleted == statusTcs.Task, "statusCode port must NOT fire on timeout");
    }

    [Fact]
    public async Task ConnectionFailure_PublishesToErrorPort_BodyAndStatusCodeNotTriggered()
    {
        // Arrange — handler throws HttpRequestException to simulate network error
        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
            throw new HttpRequestException("Connection refused"));

        var factory = CreateHttpClientFactory(fakeHandler);
        var fixture = await CreateModuleAsync(factory, new Dictionary<string, string>
        {
            ["url"] = "http://example.com/unreachable",
            ["method"] = "GET",
            ["headers"] = "",
            ["body"] = ""
        });

        var errorTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.error",
            (evt, ct) => { errorTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.body",
            (evt, ct) => { bodyTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.statusCode",
            (evt, ct) => { statusTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });

        // Act
        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.trigger",
            Payload = ""
        });

        // Assert
        var errorCompleted = await Task.WhenAny(errorTcs.Task, Task.Delay(5000));
        Assert.True(errorCompleted == errorTcs.Task, "Expected error port to fire on connection failure");
        Assert.Contains("ConnectionFailed", await errorTcs.Task);

        var bodyCompleted = await Task.WhenAny(bodyTcs.Task, Task.Delay(200));
        Assert.False(bodyCompleted == bodyTcs.Task, "body port must NOT fire on connection failure");

        var statusCompleted = await Task.WhenAny(statusTcs.Task, Task.Delay(200));
        Assert.False(statusCompleted == statusTcs.Task, "statusCode port must NOT fire on connection failure");
    }

    [Fact]
    public async Task EmptyUrl_PublishesToErrorPort_WithMissingUrlReason()
    {
        // Arrange — no URL configured
        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

        var factory = CreateHttpClientFactory(fakeHandler);
        var fixture = await CreateModuleAsync(factory, new Dictionary<string, string>
        {
            ["url"] = "",   // deliberately empty
            ["method"] = "GET",
            ["headers"] = "",
            ["body"] = ""
        });

        var errorTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.error",
            (evt, ct) => { errorTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.body",
            (evt, ct) => { bodyTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });

        // Act
        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.trigger",
            Payload = ""
        });

        // Assert
        var errorCompleted = await Task.WhenAny(errorTcs.Task, Task.Delay(5000));
        Assert.True(errorCompleted == errorTcs.Task, "Expected error port to fire on empty URL");
        Assert.Contains("MissingUrl", await errorTcs.Task);

        var bodyCompleted = await Task.WhenAny(bodyTcs.Task, Task.Delay(200));
        Assert.False(bodyCompleted == bodyTcs.Task, "body port must NOT fire when URL is empty");
    }

    [Fact]
    public async Task BodyInputPort_IsBuffered_UsedInPostRequest()
    {
        // Arrange — fake handler captures the request body content
        string? capturedBody = null;

        var fakeHandler = new FakeHttpMessageHandler(async (req, ct) =>
        {
            if (req.Content != null)
            {
                capturedBody = await req.Content.ReadAsStringAsync(ct);
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
        });

        var factory = CreateHttpClientFactory(fakeHandler);
        var fixture = await CreateModuleAsync(factory, new Dictionary<string, string>
        {
            ["url"] = "http://example.com/api",
            ["method"] = "POST",
            ["headers"] = "",
            ["body"] = ""
        });

        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.EventBus.Subscribe<string>("HttpRequestModule.port.statusCode",
            (evt, ct) => { statusTcs.TrySetResult(evt.Payload ?? ""); return Task.CompletedTask; });

        // Act — publish body payload first (buffers in module), then trigger
        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.body",
            Payload = "test payload"
        });

        // Brief wait for async body subscription handler to execute before trigger
        await Task.Delay(50);

        await fixture.EventBus.PublishAsync(new ModuleEvent<string>
        {
            EventName = "HttpRequestModule.port.trigger",
            Payload = ""
        });

        // Assert — wait for response
        var statusCompleted = await Task.WhenAny(statusTcs.Task, Task.Delay(5000));
        Assert.True(statusCompleted == statusTcs.Task, "Expected statusCode port to fire");
        Assert.Equal("200", await statusTcs.Task);

        // Verify the buffered body was sent in the request
        Assert.Equal("test payload", capturedBody);
    }
}
