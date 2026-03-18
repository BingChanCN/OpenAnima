using OpenAnima.Contracts;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for ModuleStorageService — verifies path conventions, auto-creation, and validation.
/// </summary>
[Trait("Category", "ModuleStorage")]
public class ModuleStorageServiceTests : IDisposable
{
    private readonly string _animasRoot;
    private readonly string _dataRoot;
    private readonly FakeModuleContext _context;

    public ModuleStorageServiceTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), $"oa-storage-test-{Guid.NewGuid():N}");
        _animasRoot = Path.Combine(_dataRoot, "animas");
        Directory.CreateDirectory(_animasRoot);
        _context = new FakeModuleContext { ActiveAnimaId = "anima-1" };
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
            Directory.Delete(_dataRoot, recursive: true);
    }

    private ModuleStorageService CreateService(string? boundModuleId = null)
        => new ModuleStorageService(_animasRoot, _dataRoot, _context, boundModuleId);

    // -----------------------------------------------------------------------
    // GetDataDirectory(string moduleId) — path convention
    // -----------------------------------------------------------------------

    [Fact]
    public void GetDataDirectory_ReturnsPath_UnderAnimasRoot_WithActiveAnimaId()
    {
        var svc = CreateService();
        var path = svc.GetDataDirectory("MyModule");
        Assert.EndsWith(
            Path.Combine("anima-1", "module-data", "MyModule"),
            path);
    }

    [Fact]
    public void GetDataDirectory_PathChanges_WhenActiveAnimaIdChanges()
    {
        var svc = CreateService();
        var path1 = svc.GetDataDirectory("MyModule");

        _context.ActiveAnimaId = "anima-2";
        var path2 = svc.GetDataDirectory("MyModule");

        Assert.Contains("anima-1", path1);
        Assert.Contains("anima-2", path2);
        Assert.NotEqual(path1, path2);
    }

    [Fact]
    public void GetDataDirectory_AutoCreates_Directory()
    {
        var svc = CreateService();
        var path = svc.GetDataDirectory("MyModule");
        Assert.True(Directory.Exists(path));
    }

    // -----------------------------------------------------------------------
    // GetDataDirectory() no-arg — bound module id
    // -----------------------------------------------------------------------

    [Fact]
    public void GetDataDirectory_NoArg_WithBoundModuleId_ReturnsSameAsExplicit()
    {
        var svc = CreateService(boundModuleId: "BoundModule");
        var noArg = svc.GetDataDirectory();
        var explicit_ = svc.GetDataDirectory("BoundModule");
        Assert.Equal(explicit_, noArg);
    }

    [Fact]
    public void GetDataDirectory_NoArg_WithoutBoundModuleId_ThrowsInvalidOperationException()
    {
        var svc = CreateService();
        Assert.Throws<InvalidOperationException>(() => svc.GetDataDirectory());
    }

    // -----------------------------------------------------------------------
    // GetGlobalDataDirectory(string moduleId) — path convention
    // -----------------------------------------------------------------------

    [Fact]
    public void GetGlobalDataDirectory_ReturnsPath_UnderDataRoot_ModuleData()
    {
        var svc = CreateService();
        var path = svc.GetGlobalDataDirectory("MyModule");
        Assert.EndsWith(
            Path.Combine("module-data", "MyModule"),
            path);
        Assert.DoesNotContain("animas", path.Substring(_dataRoot.Length));
    }

    [Fact]
    public void GetGlobalDataDirectory_AutoCreates_Directory()
    {
        var svc = CreateService();
        var path = svc.GetGlobalDataDirectory("MyModule");
        Assert.True(Directory.Exists(path));
    }

    // -----------------------------------------------------------------------
    // Validation — invalid moduleId
    // -----------------------------------------------------------------------

    [Fact]
    public void GetDataDirectory_DotDot_ThrowsArgumentException()
    {
        var svc = CreateService();
        Assert.Throws<ArgumentException>(() => svc.GetDataDirectory("../evil"));
    }

    [Fact]
    public void GetDataDirectory_ForwardSlash_ThrowsArgumentException()
    {
        var svc = CreateService();
        Assert.Throws<ArgumentException>(() => svc.GetDataDirectory("evil/path"));
    }

    [Fact]
    public void GetDataDirectory_BackSlash_ThrowsArgumentException()
    {
        var svc = CreateService();
        Assert.Throws<ArgumentException>(() => svc.GetDataDirectory("evil\\path"));
    }

    [Fact]
    public void GetDataDirectory_Empty_ThrowsArgumentException()
    {
        var svc = CreateService();
        Assert.Throws<ArgumentException>(() => svc.GetDataDirectory(""));
    }

    [Fact]
    public void GetDataDirectory_Null_ThrowsArgumentException()
    {
        var svc = CreateService();
        Assert.Throws<ArgumentException>(() => svc.GetDataDirectory(null!));
    }

    [Fact]
    public void GetDataDirectory_DotsAllowed_Succeeds()
    {
        var svc = CreateService();
        var path = svc.GetDataDirectory("My.Valid.Module");
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void GetDataDirectory_HyphensAllowed_Succeeds()
    {
        var svc = CreateService();
        var path = svc.GetDataDirectory("my-module");
        Assert.True(Directory.Exists(path));
    }

    // -----------------------------------------------------------------------
    // Helper: fake IModuleContext
    // -----------------------------------------------------------------------

    private class FakeModuleContext : IModuleContext
    {
        public string ActiveAnimaId { get; set; } = "";
        public event Action? ActiveAnimaChanged;
    }
}
