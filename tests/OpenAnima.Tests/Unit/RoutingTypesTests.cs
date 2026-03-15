using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Routing;

namespace OpenAnima.Tests.Unit;

[Trait("Category", "Routing")]
public class RoutingTypesTests
{
    // RouteResult tests

    [Fact]
    public void RouteResult_Ok_HasIsSuccessTrue()
    {
        var result = RouteResult.Ok("payload", "correlationId");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void RouteResult_Ok_HasPayloadAndCorrelationId()
    {
        var result = RouteResult.Ok("hello", "abc123");
        Assert.Equal("hello", result.Payload);
        Assert.Equal("abc123", result.CorrelationId);
        Assert.Null(result.Error);
    }

    [Fact]
    public void RouteResult_Failed_Timeout_HasIsSuccessFalse()
    {
        var result = RouteResult.Failed(RouteErrorKind.Timeout, "cid");
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteErrorKind.Timeout, result.Error);
        Assert.Equal("cid", result.CorrelationId);
    }

    [Fact]
    public void RouteResult_NotFound_HasIsSuccessFalse()
    {
        var result = RouteResult.NotFound("a1::port");
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteErrorKind.NotFound, result.Error);
    }

    [Fact]
    public void RouteErrorKind_HasExactlyFourValues()
    {
        var values = Enum.GetValues<RouteErrorKind>();
        Assert.Equal(4, values.Length);
        Assert.Contains(RouteErrorKind.Timeout, values);
        Assert.Contains(RouteErrorKind.NotFound, values);
        Assert.Contains(RouteErrorKind.Cancelled, values);
        Assert.Contains(RouteErrorKind.Failed, values);
    }

    // RouteRegistrationResult tests

    [Fact]
    public void RouteRegistrationResult_Success_HasIsSuccessTrueAndNullError()
    {
        var result = RouteRegistrationResult.Success();
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void RouteRegistrationResult_DuplicateError_HasIsSuccessFalseAndErrorMessage()
    {
        var result = RouteRegistrationResult.DuplicateError("already registered");
        Assert.False(result.IsSuccess);
        Assert.Equal("already registered", result.Error);
    }

    // PortRegistration record tests

    [Fact]
    public void PortRegistration_HasRequiredProperties()
    {
        var reg = new PortRegistration("animaId", "portName", "description");
        Assert.Equal("animaId", reg.AnimaId);
        Assert.Equal("portName", reg.PortName);
        Assert.Equal("description", reg.Description);
    }

    // PendingRequest record tests

    [Fact]
    public void PendingRequest_HasRequiredProperties()
    {
        var tcs = new TaskCompletionSource<RouteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource();
        var expires = DateTimeOffset.UtcNow.AddSeconds(30);
        var pending = new PendingRequest("cid", tcs, cts, expires, "animaId");

        Assert.Equal("cid", pending.CorrelationId);
        Assert.Same(tcs, pending.Tcs);
        Assert.Same(cts, pending.Cts);
        Assert.Equal(expires, pending.ExpiresAt);
        Assert.Equal("animaId", pending.TargetAnimaId);

        cts.Dispose();
    }
}
