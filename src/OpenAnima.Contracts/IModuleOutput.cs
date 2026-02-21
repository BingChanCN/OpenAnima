namespace OpenAnima.Contracts;

/// <summary>
/// Generic marker interface for typed output ports.
/// A module implementing IModuleOutput&lt;T&gt; declares it produces outputs of type T.
/// Example: IModuleOutput&lt;ChatResponse&gt; means the module emits ChatResponse outputs.
/// The event-based pattern prepares for Phase 2 event bus wiring.
/// </summary>
/// <typeparam name="T">The output data type this module produces.</typeparam>
public interface IModuleOutput<T>
{
    /// <summary>
    /// Event fired when the module produces an output of type T.
    /// The event bus will subscribe to this and route to matching IModuleInput&lt;T&gt; consumers.
    /// </summary>
    event Func<T, CancellationToken, Task>? OnOutput;
}
