# Quick-Start Guide

**Time to complete:** 5 minutes

Create your first OpenAnima module from scratch and see it running in the runtime.

## Prerequisites

- .NET 8 SDK installed
- `oani` CLI tool installed (see main README for installation)

## Step 1: Create Module (30 seconds)

Run the `oani new` command to create a new module project:

```bash
oani new HelloModule
cd HelloModule
```

**Expected output:**
```
Module project created at: ./HelloModule
```

## Step 2: Implement Logic (2 minutes)

Open `HelloModule.cs` and replace the contents with this complete implementation:

```csharp
using Microsoft.Extensions.Logging;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Ports;

namespace HelloModule;

/// <summary>
/// A simple module that publishes a greeting message.
/// This is a "source" module — it has no input ports.
/// </summary>
[OutputPort("greeting", PortType.Text)]
public class HelloModule : IModuleExecutor
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<HelloModule> _logger;
    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    // Module metadata: name, version, description
    public IModuleMetadata Metadata { get; } = new ModuleMetadataRecord(
        "HelloModule", "1.0.0", "Publishes a greeting message");

    public HelloModule(IEventBus eventBus, ILogger<HelloModule> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("HelloModule initialized");
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _state = ModuleExecutionState.Running;
        _lastError = null;

        try
        {
            // Publish greeting message to output port
            await _eventBus.PublishAsync(new ModuleEvent<string>
            {
                EventName = $"{Metadata.Name}.port.greeting",
                SourceModuleId = Metadata.Name,
                Payload = "Hello, OpenAnima!"
            }, ct);

            _state = ModuleExecutionState.Completed;
            _logger.LogInformation("HelloModule published greeting");
        }
        catch (Exception ex)
        {
            _state = ModuleExecutionState.Error;
            _lastError = ex;
            _logger.LogError(ex, "HelloModule failed");
            throw;
        }
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("HelloModule shutdown");
        return Task.CompletedTask;
    }

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;
}
```

**What this code does:**
- Declares one output port named "greeting" (line 11)
- Implements `IModuleExecutor` interface (required for all modules)
- Publishes "Hello, OpenAnima!" message when executed (lines 40-45)
- Tracks execution state (Idle → Running → Completed)

## Step 3: Build (30 seconds)

Build the module project:

```bash
dotnet build
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Step 4: Pack (30 seconds)

Package the module into `.oamod` format:

```bash
oani pack .
```

**Expected output:**
```
Module packed successfully: HelloModule.oamod
```

You should see a `HelloModule.oamod` file in your current directory.

## Step 5: Load in Runtime (30 seconds)

Copy the `.oamod` file to the OpenAnima runtime's modules directory:

```bash
# Assuming OpenAnima runtime is at ../OpenAnima
cp HelloModule.oamod ../OpenAnima/modules/
```

Start the OpenAnima runtime. Your HelloModule will be automatically discovered and loaded.

**You should see:**
- HelloModule appears in the module list
- It has one output port: "greeting"
- When executed, it publishes "Hello, OpenAnima!" to the greeting port

## Next Steps

Now that you have a working module, explore more advanced patterns:

- **[API Reference](api-reference/README.md)** — Learn about all interfaces (IModule, IModuleExecutor, ITickable, IEventBus)
- **[Port System](api-reference/port-system.md)** — Understand input/output ports and EventBus
- **[Common Patterns](api-reference/common-patterns.md)** — See real-world examples:
  - **Source modules** (no inputs) — like HelloModule
  - **Transform modules** (input → output) — process and forward data
  - **Sink modules** (input only) — consume data without outputs
  - **Heartbeat modules** (ITickable) — periodic execution

## Troubleshooting

**Build fails with "IModuleExecutor not found":**
- Ensure you have the OpenAnima.Contracts NuGet package referenced
- Check that your .csproj has `<PackageReference Include="OpenAnima.Contracts" Version="1.0.0" />`

**Pack fails with "Assembly not found":**
- Run `dotnet build` first
- Check that `bin/Release/net8.0/HelloModule.dll` exists

**Module doesn't appear in runtime:**
- Verify the `.oamod` file is in the correct modules directory
- Check runtime logs for loading errors
- Ensure the module implements `IModule` or `IModuleExecutor`
