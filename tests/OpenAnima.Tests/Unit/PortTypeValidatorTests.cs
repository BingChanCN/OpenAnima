using OpenAnima.Contracts.Ports;
using OpenAnima.Core.Ports;

namespace OpenAnima.Tests.Unit;

public class PortTypeValidatorTests
{
    private readonly PortTypeValidator _validator = new();

    [Fact]
    public void ValidConnection_SameType_ReturnsSuccess()
    {
        // Arrange
        var source = new PortMetadata("out1", PortType.Text, PortDirection.Output, "ModuleA");
        var target = new PortMetadata("in1", PortType.Text, PortDirection.Input, "ModuleB");

        // Act
        var result = _validator.ValidateConnection(source, target);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void InvalidConnection_DifferentTypes_ReturnsFail()
    {
        // Arrange
        var source = new PortMetadata("out1", PortType.Text, PortDirection.Output, "ModuleA");
        var target = new PortMetadata("in1", PortType.Trigger, PortDirection.Input, "ModuleB");

        // Act
        var result = _validator.ValidateConnection(source, target);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Text", result.ErrorMessage);
        Assert.Contains("Trigger", result.ErrorMessage);
    }

    [Fact]
    public void InvalidConnection_OutputToOutput_ReturnsFail()
    {
        // Arrange
        var source = new PortMetadata("out1", PortType.Text, PortDirection.Output, "ModuleA");
        var target = new PortMetadata("out2", PortType.Text, PortDirection.Output, "ModuleB");

        // Act
        var result = _validator.ValidateConnection(source, target);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void InvalidConnection_InputToInput_ReturnsFail()
    {
        // Arrange
        var source = new PortMetadata("in1", PortType.Text, PortDirection.Input, "ModuleA");
        var target = new PortMetadata("in2", PortType.Text, PortDirection.Input, "ModuleB");

        // Act
        var result = _validator.ValidateConnection(source, target);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void InvalidConnection_SelfConnection_ReturnsFail()
    {
        // Arrange
        var source = new PortMetadata("out1", PortType.Text, PortDirection.Output, "ModuleA");
        var target = new PortMetadata("in1", PortType.Text, PortDirection.Input, "ModuleA");

        // Act
        var result = _validator.ValidateConnection(source, target);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ValidConnection_FanOut_AllowsMultipleFromSameOutput()
    {
        // Arrange
        var source = new PortMetadata("out1", PortType.Text, PortDirection.Output, "ModuleA");
        var target1 = new PortMetadata("in1", PortType.Text, PortDirection.Input, "ModuleB");
        var target2 = new PortMetadata("in2", PortType.Text, PortDirection.Input, "ModuleC");

        // Act
        var result1 = _validator.ValidateConnection(source, target1);
        var result2 = _validator.ValidateConnection(source, target2);

        // Assert
        Assert.True(result1.IsValid);
        Assert.True(result2.IsValid);
    }
}
