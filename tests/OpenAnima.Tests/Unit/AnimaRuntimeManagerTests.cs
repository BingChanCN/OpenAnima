using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Core.Anima;

namespace OpenAnima.Tests.Unit;

public class AnimaRuntimeManagerTests : IAsyncDisposable
{
    private readonly string _tempRoot;
    private readonly AnimaRuntimeManager _manager;
    private readonly AnimaContext _animaContext;

    public AnimaRuntimeManagerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"anima-test-{Guid.NewGuid()}");
        _animaContext = new AnimaContext();
        _manager = new AnimaRuntimeManager(
            _tempRoot,
            NullLogger<AnimaRuntimeManager>.Instance,
            NullLoggerFactory.Instance,
            _animaContext);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_PersistsDescriptorToDisk()
    {
        var descriptor = await _manager.CreateAsync("TestAnima");
        var path = Path.Combine(_tempRoot, descriptor.Id, "anima.json");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueEightCharHexId()
    {
        var d1 = await _manager.CreateAsync("Anima1");
        var d2 = await _manager.CreateAsync("Anima2");
        Assert.Equal(8, d1.Id.Length);
        Assert.Equal(8, d2.Id.Length);
        Assert.NotEqual(d1.Id, d2.Id);
    }

    [Fact]
    public async Task CreateAsync_SetsNameCorrectly()
    {
        var descriptor = await _manager.CreateAsync("MyAnima");
        Assert.Equal("MyAnima", descriptor.Name);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAtToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var descriptor = await _manager.CreateAsync("TimedAnima");
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(descriptor.CreatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_FiresStateChangedEvent()
    {
        var fired = false;
        _manager.StateChanged += () => fired = true;
        await _manager.CreateAsync("EventAnima");
        Assert.True(fired);
    }

    // --- InitializeAsync ---

    [Fact]
    public async Task InitializeAsync_LoadsExistingAnimasFromDisk()
    {
        // Create two animas, then create a fresh manager over the same root
        await _manager.CreateAsync("Anima1");
        await _manager.CreateAsync("Anima2");

        var manager2 = new AnimaRuntimeManager(_tempRoot, NullLogger<AnimaRuntimeManager>.Instance, NullLoggerFactory.Instance, new AnimaContext());
        await manager2.InitializeAsync();

        var all = manager2.GetAll();
        Assert.Equal(2, all.Count);
        await manager2.DisposeAsync();
    }

    [Fact]
    public async Task InitializeAsync_WithEmptyDirectory_ReturnsEmptyList()
    {
        await _manager.InitializeAsync();
        Assert.Empty(_manager.GetAll());
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_RemovesFromMemoryAndDisk()
    {
        var descriptor = await _manager.CreateAsync("ToDelete");
        var dir = Path.Combine(_tempRoot, descriptor.Id);
        Assert.True(Directory.Exists(dir));

        await _manager.DeleteAsync(descriptor.Id);

        Assert.Empty(_manager.GetAll());
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        await _manager.DeleteAsync("nonexistent");
    }

    [Fact]
    public async Task DeleteAsync_FiresStateChangedEvent()
    {
        var descriptor = await _manager.CreateAsync("ToDelete");
        var fired = false;
        _manager.StateChanged += () => fired = true;
        await _manager.DeleteAsync(descriptor.Id);
        Assert.True(fired);
    }

    // --- RenameAsync ---

    [Fact]
    public async Task RenameAsync_UpdatesNameInMemoryAndOnDisk()
    {
        var descriptor = await _manager.CreateAsync("OldName");
        await _manager.RenameAsync(descriptor.Id, "NewName");

        var updated = _manager.GetById(descriptor.Id);
        Assert.NotNull(updated);
        Assert.Equal("NewName", updated!.Name);

        // Verify persisted to disk
        var manager2 = new AnimaRuntimeManager(_tempRoot, NullLogger<AnimaRuntimeManager>.Instance, NullLoggerFactory.Instance, new AnimaContext());
        await manager2.InitializeAsync();
        var loaded = manager2.GetById(descriptor.Id);
        Assert.Equal("NewName", loaded!.Name);
        await manager2.DisposeAsync();
    }

    [Fact]
    public async Task RenameAsync_FiresStateChangedEvent()
    {
        var descriptor = await _manager.CreateAsync("Original");
        var fired = false;
        _manager.StateChanged += () => fired = true;
        await _manager.RenameAsync(descriptor.Id, "Renamed");
        Assert.True(fired);
    }

    // --- CloneAsync ---

    [Fact]
    public async Task CloneAsync_CreatesNewAnimaWithCopySuffix()
    {
        var original = await _manager.CreateAsync("MyAnima");
        var clone = await _manager.CloneAsync(original.Id);

        Assert.Equal("MyAnima (Copy)", clone.Name);
        Assert.NotEqual(original.Id, clone.Id);
    }

    [Fact]
    public async Task CloneAsync_PersistsCloneToDisk()
    {
        var original = await _manager.CreateAsync("Source");
        var clone = await _manager.CloneAsync(original.Id);

        var clonePath = Path.Combine(_tempRoot, clone.Id, "anima.json");
        Assert.True(File.Exists(clonePath));
    }

    [Fact]
    public async Task CloneAsync_FiresStateChangedEvent()
    {
        var original = await _manager.CreateAsync("Source");
        var fired = false;
        _manager.StateChanged += () => fired = true;
        await _manager.CloneAsync(original.Id);
        Assert.True(fired);
    }

    // --- GetAll ---

    [Fact]
    public async Task GetAll_ReturnsAnimasOrderedByCreatedAt()
    {
        var a1 = await _manager.CreateAsync("First");
        await Task.Delay(5); // ensure distinct timestamps
        var a2 = await _manager.CreateAsync("Second");

        var all = _manager.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal(a1.Id, all[0].Id);
        Assert.Equal(a2.Id, all[1].Id);
    }

    // --- DisposeAsync ---

    [Fact]
    public async Task DisposeAsync_DisposesWithoutError()
    {
        var manager = new AnimaRuntimeManager(_tempRoot, NullLogger<AnimaRuntimeManager>.Instance, NullLoggerFactory.Instance, new AnimaContext());
        await manager.DisposeAsync(); // should not throw
    }

    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}

public class AnimaContextTests
{
    [Fact]
    public void SetActive_SetsActiveAnimaId()
    {
        var context = new AnimaContext();
        context.SetActive("anima-1");
        Assert.Equal("anima-1", context.ActiveAnimaId);
    }

    [Fact]
    public void SetActive_FiresActiveAnimaChangedEvent()
    {
        var context = new AnimaContext();
        var fired = false;
        context.ActiveAnimaChanged += () => fired = true;
        context.SetActive("anima-1");
        Assert.True(fired);
    }

    [Fact]
    public void SetActive_WithSameId_DoesNotFireEvent()
    {
        var context = new AnimaContext();
        context.SetActive("anima-1");
        var fireCount = 0;
        context.ActiveAnimaChanged += () => fireCount++;
        context.SetActive("anima-1"); // same ID — should not fire
        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void SetActive_WithDifferentId_FiresEvent()
    {
        var context = new AnimaContext();
        context.SetActive("anima-1");
        var fired = false;
        context.ActiveAnimaChanged += () => fired = true;
        context.SetActive("anima-2");
        Assert.True(fired);
    }
}
