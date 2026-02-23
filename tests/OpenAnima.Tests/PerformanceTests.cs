using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Events;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Runtime;
using OpenAnima.Tests.TestHelpers;
using Xunit;

namespace OpenAnima.Tests;

/// <summary>
/// Performance validation tests for sustained heartbeat operation with multiple modules.
/// </summary>
public class PerformanceTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task HeartbeatLoop_MaintainsPerformance_With20Modules()
    {
        // Arrange
        string testModulesPath = Path.Combine(Path.GetTempPath(), "OpenAnima_PerfTest_" + Guid.NewGuid());
        Directory.CreateDirectory(testModulesPath);

        try
        {
            // Create 20 test modules
            var loader = new PluginLoader();
            var registry = new PluginRegistry();
            var loadedModules = new List<string>();

            for (int i = 0; i < 20; i++)
            {
                string moduleName = $"TestModule{i:D2}";
                string moduleDir = ModuleTestHarness.CreateTestModuleDirectory(testModulesPath, moduleName);

                var result = loader.LoadModule(moduleDir);
                Assert.True(result.Success, $"Failed to load module {moduleName}: {result.Error?.Message}");

                registry.Register(moduleName, result.Module!, result.Context!, result.Manifest!);
                loadedModules.Add(moduleName);
            }

            // Create HeartbeatLoop with minimal dependencies
            var eventBus = new EventBus(NullLogger<EventBus>.Instance);
            var heartbeat = new HeartbeatLoop(
                eventBus,
                registry,
                interval: TimeSpan.FromMilliseconds(100),
                logger: NullLogger<HeartbeatLoop>.Instance,
                hubContext: null);

            var latencySamples = new List<double>();
            var cts = new CancellationTokenSource();

            // Act: Start heartbeat and collect latency samples for 10 seconds
            await heartbeat.StartAsync(cts.Token);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(200); // Sample every 200ms
                latencySamples.Add(heartbeat.LastTickLatencyMs);
            }

            await heartbeat.StopAsync();

            // Assert: Performance thresholds
            double avgLatency = latencySamples.Average();
            double maxLatency = latencySamples.Max();

            Assert.True(avgLatency < 50,
                $"Average latency too high: {avgLatency:F2}ms (threshold: 50ms)");

            Assert.True(maxLatency < 200,
                $"Max latency too high: {maxLatency:F2}ms (threshold: 200ms)");

            // Verify heartbeat actually ran
            Assert.True(heartbeat.TickCount > 0, "Heartbeat did not tick");

            // Cleanup: Unload all modules
            foreach (var moduleName in loadedModules)
            {
                registry.Unregister(moduleName);
            }
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testModulesPath))
            {
                Directory.Delete(testModulesPath, recursive: true);
            }
        }
    }
}
