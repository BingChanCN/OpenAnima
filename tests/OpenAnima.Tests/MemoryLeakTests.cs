using OpenAnima.Core.Plugins;
using OpenAnima.Tests.TestHelpers;
using Xunit;

namespace OpenAnima.Tests;

/// <summary>
/// Memory leak validation tests for module loading and unloading.
/// </summary>
public class MemoryLeakTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void UnloadModule_ReleasesMemory_After100Cycles()
    {
        // Arrange
        string testModulesPath = Path.Combine(Path.GetTempPath(), "OpenAnima_MemoryLeakTest_" + Guid.NewGuid());
        Directory.CreateDirectory(testModulesPath);

        try
        {
            // Create a test module directory
            string moduleDir = ModuleTestHarness.CreateTestModuleDirectory(testModulesPath, "TestModule");

            var weakReferences = new List<WeakReference>();
            var loader = new PluginLoader();

            // Act: Load and unload module 100 times
            for (int i = 0; i < 100; i++)
            {
                var result = loader.LoadModule(moduleDir);

                Assert.True(result.Success, $"Module load failed on iteration {i}: {result.Error?.Message}");
                Assert.NotNull(result.Context);

                // Create weak reference to track the context
                weakReferences.Add(new WeakReference(result.Context));

                // Unload the context
                result.Context!.Unload();
            }

            // Force garbage collection multiple times
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }

            // Assert: Most contexts should be collected (< 10% leak rate)
            int aliveCount = weakReferences.Count(wr => wr.IsAlive);
            double leakRate = (double)aliveCount / weakReferences.Count;

            Assert.True(leakRate < 0.10,
                $"Memory leak detected: {aliveCount}/{weakReferences.Count} contexts still alive ({leakRate:P1} leak rate)");
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
