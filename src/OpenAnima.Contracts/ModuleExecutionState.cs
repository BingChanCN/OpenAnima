namespace OpenAnima.Contracts;

/// <summary>
/// Tracks the execution lifecycle of a module.
/// Used by IModuleExecutor to report current state to the runtime and editor.
/// </summary>
public enum ModuleExecutionState
{
    /// <summary>Module is loaded but not currently executing.</summary>
    Idle = 0,

    /// <summary>Module is actively processing (ExecuteAsync in progress).</summary>
    Running = 1,

    /// <summary>Module finished execution successfully.</summary>
    Completed = 2,

    /// <summary>Module encountered an error during execution.</summary>
    Error = 3
}
