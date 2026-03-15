namespace OpenAnima.Contracts;

/// <summary>
/// Marks a module as stateless — safe for concurrent execution without channel serialization.
/// Default (no attribute): stateful — channel-serialized execution.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class StatelessModuleAttribute : Attribute
{
}
