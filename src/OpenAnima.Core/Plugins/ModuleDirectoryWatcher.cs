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
    private FileSystemWatcher? _fileWatcher;
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
    /// Starts watching the modules directory for new subdirectories and .oamod files.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public void StartWatching()
    {
        // Create modules directory if not exists
        if (!Directory.Exists(_modulesPath))
        {
            Directory.CreateDirectory(_modulesPath);
        }

        // Initialize directory watcher
        _watcher = new FileSystemWatcher(_modulesPath)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnDirectoryCreated;
        _watcher.EnableRaisingEvents = true;

        // Initialize .oamod file watcher
        _fileWatcher = new FileSystemWatcher(_modulesPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*.oamod",
            IncludeSubdirectories = false
        };

        _fileWatcher.Created += OnOamodCreated;
        _fileWatcher.Changed += OnOamodCreated;
        _fileWatcher.EnableRaisingEvents = true;
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

        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Created -= OnOamodCreated;
            _fileWatcher.Changed -= OnOamodCreated;
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
    /// Manually re-scans the modules directory and invokes the callback for any new (untracked) directories and .oamod files.
    /// Useful as a fallback if FileSystemWatcher events are missed.
    /// </summary>
    public void RefreshAll()
    {
        if (!Directory.Exists(_modulesPath))
        {
            return;
        }

        // Scan directories
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

        // Scan .oamod files
        foreach (string oamodFile in Directory.GetFiles(_modulesPath, "*.oamod"))
        {
            lock (_lock)
            {
                if (!_discoveredPaths.Contains(oamodFile))
                {
                    _discoveredPaths.Add(oamodFile);

                    // Extract and discover
                    try
                    {
                        var extractedDir = OamodExtractor.Extract(oamodFile, _modulesPath);
                        _onModuleDiscovered(extractedDir);
                    }
                    catch
                    {
                        // Ignore extraction errors during refresh
                    }
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

    private void OnOamodCreated(object sender, FileSystemEventArgs e)
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

                // Extract and invoke callback outside lock
                try
                {
                    var extractedDir = OamodExtractor.Extract(fullPath, _modulesPath);
                    _onModuleDiscovered(extractedDir);
                }
                catch
                {
                    // Log but don't crash on extraction failure (file might still be copying)
                }
            }, null, 500, Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        StopWatching();
        _watcher?.Dispose();
        _fileWatcher?.Dispose();
    }
}
