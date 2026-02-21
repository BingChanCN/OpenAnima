namespace OpenAnima.Contracts;

/// <summary>
/// Generic marker interface for typed input ports.
/// A module implementing IModuleInput&lt;T&gt; declares it accepts inputs of type T.
/// Example: IModuleInput&lt;ChatMessage&gt; means the module can process ChatMessage inputs.
/// </summary>
/// <typeparam name="T">The input data type this module accepts.</typeparam>
public interface IModuleInput<T>
{
    /// <summary>
    /// Process an input of type T.
    /// Called by the event bus when a matching output is produced.
    /// </summary>
    Task ProcessAsync(T input, CancellationToken cancellationToken = default);
}
