using OpenAnima.Contracts.Ports;

namespace OpenAnima.Core.Ports;

/// <summary>
/// Validates port connections for type compatibility and direction correctness.
/// </summary>
public class PortTypeValidator
{
    /// <summary>
    /// Validates a connection between two ports.
    /// </summary>
    /// <param name="source">Source port (must be Output)</param>
    /// <param name="target">Target port (must be Input)</param>
    /// <returns>Validation result with success status and error message if invalid</returns>
    public ValidationResult ValidateConnection(PortMetadata source, PortMetadata target)
    {
        // Check direction: source must be Output, target must be Input
        if (source.Direction != PortDirection.Output)
        {
            return ValidationResult.Fail("Source port must be an Output port");
        }

        if (target.Direction != PortDirection.Input)
        {
            return ValidationResult.Fail("Target port must be an Input port");
        }

        // Check self-connection: cannot connect ports on the same module
        if (source.ModuleName == target.ModuleName)
        {
            return ValidationResult.Fail("Cannot connect ports on the same module");
        }

        // Check type match: port types must be identical
        if (source.Type != target.Type)
        {
            return ValidationResult.Fail($"{source.Type} port cannot connect to {target.Type} port");
        }

        return ValidationResult.Success();
    }
}
