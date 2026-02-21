namespace OpenAnima.Core.Plugins;

/// <summary>
/// Monitors the modules directory for new module folders using FileSystemWatcher.
/// Provides debouncing to let file operations complete and duplicate prevention.
/// </summary>
public class ModuleDirectoryWatcher : IDisposable
{
    private readonly string _modulesPath;
    private readonly Action<string> _onModuleDiscovered;
    private readonly HashSet<string> _discoveredPaths = new();
    private FileSystemWatcher? _watcher;
    private readonly Dictionary<string, Timer> _debounceTimers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new directory watcher for hot module discovery.
    /// </summary>
    /// <param name="modulesPath">Path to the modules directory to monitor</param>
    /// <param name="onModuleDiscovered">Callback invoked when a new module directory is detected</param>
    public ModuleDirectoryWatcher(string modulesPath, Action<string> onModuleDiscovered)
    {
        _modulesPath = modulesPath;
        _onModuleDiscovered = onModuleDiscovered;
    }

    /// <summary>
    /// Starts watching the modules directory for new subdirectories.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public void StartWatching()
    {
        // Create modules directory if not exists
        if (!Directory.Exists(_modulesPath))
        {
            Directory.CreateDirectory(_modulesPath);
        }

        // Initialize watcher
        _watcher = new FileSystemWatcher(_modulesPath)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnDirectoryCreated;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Stops watching the modules directory.
    /// </summary>
    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnDirectoryCreated;
        }

        // Dispose all pending timers
        lock (_lock)
        {
            foreach (var timer in _debounceTimers.Values)
            {
                timer.Dispose();
            }
            _debounceTimers.Clear();
        }
    }

    /// <summary>
    /// Manually re-scans the modules directory and invokes the callback for any new (untracked) directories.
    /// Useful as a fallback if FileSystemWatcher events are missed.
    /// </summary>
    public void RefreshAll()
    {
        if (!Directory.Exists(_modulesPath))
        {
            return;
        }

        foreach (string subdirectory in Directory.GetDirectories(_modulesPath))
        {
            lock (_lock)
            {
                if (!_discoveredPaths.Contains(subdirectory))
                {
                    _discoveredPaths.Add(subdirectory);
                    _onModuleDiscovered(subdirectory);
                }
            }
        }
    }

    private void OnDirectoryCreated(object sender, FileSystemEventArgs e)
    {
        string fullPath = e.FullPath;

        lock (_lock)
        {
            // Dispose existing timer for this path if any
            if (_debounceTimers.TryGetValue(fullPath, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            // Create new debounce timer (500ms delay)
            _debounceTimers[fullPath] = new Timer(_ =>
            {
                lock (_lock)
                {
                    // Remove timer
                    if (_debounceTimers.TryGetValue(fullPath, out var timer))
                    {
                        timer.Dispose();
                        _debounceTimers.Remove(fullPath);
                    }

                    // Check if already discovered (prevent duplicates)
                    if (_discoveredPaths.Contains(fullPath))
                    {
                        return;
                    }

                    _discoveredPaths.Add(fullPath);
                }

                // Invoke callback outside lock
                _onModuleDiscovered(fullPath);
            }, null, 500, Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        StopWatching();
        _watcher?.Dispose();
    }
}
