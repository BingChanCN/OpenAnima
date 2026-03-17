using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Plugins;
using OpenAnima.Tests.TestHelpers;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration tests for PluginLoader DI injection (PLUG-01, PLUG-02, PLUG-03).
/// Validates that external modules receive Contracts services via constructor injection.
/// </summary>
public class PluginLoaderDITests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginLoader _loader;

    public PluginLoaderDITests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oatest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _loader = new PluginLoader();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// PLUG-01: External module with Contracts services loads successfully.
    /// Module with IModuleConfig+IModuleContext+IEventBus+ICrossAnimaRouter constructor
    /// receives all services non-null.
    /// </summary>
    [Fact]
    public void ExternalModule_WithContractsServices_LoadsSuccessfully()
    {
        // Arrange
        string moduleName = "TestContractsModule";
        string moduleDir = ModuleTestHarness.CreateTestModuleWithAllContracts(_tempDir, moduleName);

        var services = new ServiceCollection();
        services.AddSingleton<IModuleConfig>(new FakeModuleConfig());
        services.AddSingleton<IModuleContext>(new FakeModuleContext());
        services.AddSingleton<IEventBus>(new FakeEventBus());
        services.AddSingleton<ICrossAnimaRouter>(new FakeCrossAnimaRouter());
        services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(builder => builder.AddConsole()));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = _loader.LoadModule(moduleDir, serviceProvider);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error?.Message}");
        Assert.NotNull(result.Module);

        // Verify injected services via reflection
        var moduleType = result.Module.GetType();
        var configProperty = moduleType.GetProperty("InjectedConfig");
        var contextProperty = moduleType.GetProperty("InjectedContext");
        var eventBusProperty = moduleType.GetProperty("InjectedEventBus");
        var routerProperty = moduleType.GetProperty("InjectedRouter");
        var loggerProperty = moduleType.GetProperty("InjectedLogger");

        Assert.NotNull(configProperty);
        Assert.NotNull(contextProperty);
        Assert.NotNull(eventBusProperty);
        Assert.NotNull(routerProperty);
        Assert.NotNull(loggerProperty);

        Assert.NotNull(configProperty.GetValue(result.Module));
        Assert.NotNull(contextProperty.GetValue(result.Module));
        Assert.NotNull(eventBusProperty.GetValue(result.Module));
        Assert.NotNull(routerProperty.GetValue(result.Module));
        Assert.NotNull(loggerProperty.GetValue(result.Module));
    }

    /// <summary>
    /// PLUG-02: External module receives ILogger via ILoggerFactory.CreateLogger(moduleType.FullName).
    /// </summary>
    [Fact]
    public void ExternalModule_ReceivesILogger_ViaFactory()
    {
        // Arrange
        string moduleName = "TestLoggerModule";
        string moduleDir = ModuleTestHarness.CreateTestModuleWithConstructor(
            _tempDir, moduleName, new[] { "ILogger" }, new[] { "logger" });

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(builder => builder.AddConsole()));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = _loader.LoadModule(moduleDir, serviceProvider);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error?.Message}");
        Assert.NotNull(result.Module);

        // Verify logger was injected
        var moduleType = result.Module.GetType();
        var loggerProperty = moduleType.GetProperty("InjectedLogger");
        Assert.NotNull(loggerProperty);
        Assert.NotNull(loggerProperty.GetValue(result.Module));
    }

    /// <summary>
    /// PLUG-03a: Module with unresolvable optional Contracts parameter loads successfully with null injected.
    /// </summary>
    [Fact]
    public void Module_OptionalParameter_LoadsWithNull()
    {
        // Arrange
        string moduleName = "TestOptionalModule";
        string moduleDir = ModuleTestHarness.CreateTestModuleWithAllContracts(_tempDir, moduleName);

        // Build ServiceProvider with NO services registered (empty)
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = _loader.LoadModule(moduleDir, serviceProvider);

        // Assert - should succeed because all params are optional
        Assert.True(result.Success, $"Expected success but got error: {result.Error?.Message}");
        Assert.NotNull(result.Module);

        // Verify injected services are null
        var moduleType = result.Module.GetType();
        var configProperty = moduleType.GetProperty("InjectedConfig");
        Assert.NotNull(configProperty);
        Assert.Null(configProperty.GetValue(result.Module));
    }

    /// <summary>
    /// PLUG-03b: Module with unresolvable required non-Contracts parameter fails with descriptive error.
    /// </summary>
    [Fact]
    public void Module_RequiredParameter_FailsWithError()
    {
        // Arrange
        string moduleName = "TestRequiredParamModule";
        string moduleDir = ModuleTestHarness.CreateTestModuleWithRequiredParam(
            _tempDir, moduleName, "ICustomService");

        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = _loader.LoadModule(moduleDir, serviceProvider);

        // Assert - should fail because required param cannot be resolved
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("required", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Backward compatibility: Module with parameterless constructor still loads with null serviceProvider.
    /// </summary>
    [Fact]
    public void Module_ParameterlessConstructor_StillLoads()
    {
        // Arrange
        string moduleName = "TestParameterlessModule";
        string moduleDir = ModuleTestHarness.CreateTestModuleDirectory(_tempDir, moduleName);

        // Act - pass null serviceProvider
        var result = _loader.LoadModule(moduleDir, null);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error?.Message}");
        Assert.NotNull(result.Module);
    }

    /// <summary>
    /// Greedy constructor: Module with multiple constructors uses the one with most parameters.
    /// </summary>
    [Fact]
    public void Module_MultipleConstructors_SelectsGreediest()
    {
        // Arrange - create module with 2 constructors (1 param and 3 params)
        string moduleName = "TestGreedyModule";
        string moduleDir = CreateTestModuleWithMultipleConstructors(_tempDir, moduleName);

        var services = new ServiceCollection();
        services.AddSingleton<IModuleConfig>(new FakeModuleConfig());
        services.AddSingleton<IModuleContext>(new FakeModuleContext());
        services.AddSingleton<IEventBus>(new FakeEventBus());
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = _loader.LoadModule(moduleDir, serviceProvider);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error?.Message}");
        Assert.NotNull(result.Module);

        // Verify the 3-param constructor was used (all 3 properties should be non-null)
        var moduleType = result.Module.GetType();
        var configProperty = moduleType.GetProperty("InjectedConfig");
        var contextProperty = moduleType.GetProperty("InjectedContext");
        var eventBusProperty = moduleType.GetProperty("InjectedEventBus");
        var constructorUsedProperty = moduleType.GetProperty("ConstructorUsed");

        Assert.NotNull(configProperty);
        Assert.NotNull(contextProperty);
        Assert.NotNull(eventBusProperty);
        Assert.NotNull(constructorUsedProperty);

        Assert.NotNull(configProperty.GetValue(result.Module));
        Assert.NotNull(contextProperty.GetValue(result.Module));
        Assert.NotNull(eventBusProperty.GetValue(result.Module));
        Assert.Equal("3-param", constructorUsedProperty.GetValue(result.Module));
    }

    // ── Fake implementations for testing ─────────────────────────────────

    private class FakeModuleConfig : IModuleConfig
    {
        public Dictionary<string, string> GetConfig(string animaId, string moduleId) => new();
        public Task SetConfigAsync(string animaId, string moduleId, string key, string value) => Task.CompletedTask;
    }

    private class FakeModuleContext : IModuleContext
    {
        public string ActiveAnimaId => "test-anima-id";
        public event Action? ActiveAnimaChanged;
    }

    private class FakeEventBus : IEventBus
    {
        public Task PublishAsync<TPayload>(ModuleEvent<TPayload> evt, CancellationToken ct = default) => Task.CompletedTask;
        public Task<TResponse> SendAsync<TResponse>(string targetModuleId, object request, CancellationToken ct = default)
            => Task.FromResult(default(TResponse)!);
        public IDisposable Subscribe<TPayload>(
            string eventName,
            Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
            Func<ModuleEvent<TPayload>, bool>? filter = null) => new FakeSubscription();
        public IDisposable Subscribe<TPayload>(
            Func<ModuleEvent<TPayload>, CancellationToken, Task> handler,
            Func<ModuleEvent<TPayload>, bool>? filter = null) => new FakeSubscription();
    }

    private class FakeSubscription : IDisposable
    {
        public void Dispose() { }
    }

    private class FakeCrossAnimaRouter : ICrossAnimaRouter
    {
        public RouteRegistrationResult RegisterPort(string animaId, string portName, string description)
            => new RouteRegistrationResult(true, null);
        public void UnregisterPort(string animaId, string portName) { }
        public IReadOnlyList<PortRegistration> GetPortsForAnima(string animaId) => new List<PortRegistration>();
        public Task<RouteResult> RouteRequestAsync(
            string targetAnimaId,
            string portName,
            string payload,
            TimeSpan? timeout = null,
            CancellationToken ct = default)
            => Task.FromResult(RouteResult.NotFound($"{targetAnimaId}::{portName}"));
        public bool CompleteRequest(string correlationId, string responsePayload) => false;
        public void CancelPendingForAnima(string animaId) { }
        public void UnregisterAllForAnima(string animaId) { }
        public void Dispose() { }
    }

    /// <summary>
    /// Helper to create a test module with multiple constructors for greedy selection testing.
    /// </summary>
    private static string CreateTestModuleWithMultipleConstructors(string basePath, string moduleName)
    {
        string moduleDir = Path.Combine(basePath, moduleName);
        Directory.CreateDirectory(moduleDir);

        var manifest = new
        {
            name = moduleName,
            version = "1.0.0",
            description = $"Test module {moduleName} with multiple constructors",
            entryAssembly = $"{moduleName}.dll"
        };

        string manifestPath = Path.Combine(moduleDir, "module.json");
        File.WriteAllText(manifestPath, System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        string dllPath = Path.Combine(moduleDir, $"{moduleName}.dll");
        CreateModuleDllWithMultipleConstructors(dllPath, moduleName);

        return moduleDir;
    }

    private static void CreateModuleDllWithMultipleConstructors(string dllPath, string moduleName)
    {
        var contractsAssembly = typeof(IModule).Assembly;
        var contractsPath = contractsAssembly.Location;
        var contractsDir = Path.GetDirectoryName(contractsPath)!;

        var loggingAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Microsoft.Extensions.Logging.Abstractions");
        var loggingPath = loggingAssembly?.Location ?? Path.Combine(contractsDir, "Microsoft.Extensions.Logging.Abstractions.dll");

        string sourceCode = $@"
using System.Threading;
using System.Threading.Tasks;
using OpenAnima.Contracts;

namespace {moduleName}
{{
    public class Metadata : IModuleMetadata
    {{
        public string Name => ""{moduleName}"";
        public string Version => ""1.0.0"";
        public string Description => ""Test module with multiple constructors"";
    }}

    public class Module : IModule
    {{
        private readonly IModuleMetadata _metadata = new Metadata();
        private readonly IModuleConfig? _config;
        private readonly IModuleContext? _context;
        private readonly IEventBus? _eventBus;
        private readonly string _constructorUsed;

        public IModuleMetadata Metadata => _metadata;
        public IModuleConfig? InjectedConfig => _config;
        public IModuleContext? InjectedContext => _context;
        public IEventBus? InjectedEventBus => _eventBus;
        public string ConstructorUsed => _constructorUsed;

        // 1-parameter constructor
        public Module(IModuleConfig config)
        {{
            _config = config;
            _constructorUsed = ""1-param"";
        }}

        // 3-parameter constructor (greedy - should be selected)
        public Module(IModuleConfig config, IModuleContext context, IEventBus eventBus)
        {{
            _config = config;
            _context = context;
            _eventBus = eventBus;
            _constructorUsed = ""3-param"";
        }}

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {{
            return Task.CompletedTask;
        }}

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {{
            return Task.CompletedTask;
        }}
    }}
}}";

        CompileSource(dllPath, moduleName, sourceCode, contractsPath, loggingPath);
    }

    private static void CompileSource(string dllPath, string moduleName, string sourceCode, string contractsPath, string? loggingPath)
    {
        string tempDir = Path.GetDirectoryName(dllPath)!;
        string sourceFile = Path.Combine(tempDir, $"{moduleName}.cs");
        File.WriteAllText(sourceFile, sourceCode);

        var loggingReference = loggingPath != null && File.Exists(loggingPath)
            ? $@"
    <Reference Include=""Microsoft.Extensions.Logging.Abstractions"">
      <HintPath>{loggingPath}</HintPath>
    </Reference>"
            : "";

        string csprojContent = $@"
<Project Sdk=""Microsoft.NET.Sdk"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>{moduleName}</AssemblyName>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include=""{moduleName}.cs"" />
    <Reference Include=""OpenAnima.Contracts"">
      <HintPath>{contractsPath}</HintPath>
    </Reference>{loggingReference}
  </ItemGroup>
</Project>";

        string csprojPath = Path.Combine(tempDir, $"{moduleName}.csproj");
        File.WriteAllText(csprojPath, csprojContent);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build -c Release -o \"{tempDir}\" /p:OutputType=Library /p:TargetFramework=net8.0",
            WorkingDirectory = tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process != null)
        {
            process.WaitForExit();
            try
            {
                File.Delete(sourceFile);
                File.Delete(csprojPath);
                foreach (string contractsFile in Directory.GetFiles(tempDir, "OpenAnima.Contracts*"))
                {
                    File.Delete(contractsFile);
                }
                foreach (string loggingFile in Directory.GetFiles(tempDir, "Microsoft.Extensions.Logging*"))
                {
                    File.Delete(loggingFile);
                }
                if (Directory.Exists(Path.Combine(tempDir, "obj")))
                {
                    Directory.Delete(Path.Combine(tempDir, "obj"), true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
