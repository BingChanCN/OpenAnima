using ContractsSsrfGuard = OpenAnima.Contracts.Http.SsrfGuard;
using CoreSsrfGuard = OpenAnima.Core.Http.SsrfGuard;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Unit tests for SsrfGuard — SSRF IP blocking utility.
/// Covers all behaviors defined in 31-01-PLAN.md.
/// </summary>
[Trait("Category", "HttpRequest")]
public class SsrfGuardTests
{
    // ── Loopback and localhost ────────────────────────────────────────────────

    [Fact]
    public void IsBlocked_Localhost_ReturnsTrueWithLoopbackReason()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("http://localhost/path", out var reason);

        Assert.True(blocked);
        Assert.True(
            reason.Contains("SsrfBlocked", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("Loopback", StringComparison.OrdinalIgnoreCase),
            $"Expected reason containing 'SsrfBlocked' or 'Loopback', got: {reason}");
    }

    [Fact]
    public void IsBlocked_127001_ReturnsTrueWithSsrfBlockedReason()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("http://127.0.0.1/api", out var reason);

        Assert.True(blocked);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void IsBlocked_127_0_0_255_ReturnsTrueFullRange()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("http://127.0.0.255/", out _);
        Assert.True(blocked);
    }

    [Fact]
    public void IsBlocked_IPv6Loopback_ReturnsTrue()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("http://[::1]/path", out _);
        Assert.True(blocked);
    }

    // ── Private RFC 1918 ranges ───────────────────────────────────────────────

    [Theory]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://10.255.255.255/")]
    public void IsBlocked_10x_Range_ReturnsTrue(string url)
    {
        var blocked = ContractsSsrfGuard.IsBlocked(url, out _);
        Assert.True(blocked, $"Expected {url} to be blocked");
    }

    [Theory]
    [InlineData("http://172.16.0.1/")]
    [InlineData("http://172.31.255.255/")]
    public void IsBlocked_172_16_31_Range_ReturnsTrue(string url)
    {
        var blocked = ContractsSsrfGuard.IsBlocked(url, out _);
        Assert.True(blocked, $"Expected {url} to be blocked");
    }

    [Fact]
    public void IsBlocked_172_15_0_1_ReturnsFalse_JustOutsideRange()
    {
        // 172.15.0.1 is OUTSIDE the 172.16.0.0/12 block — should NOT be blocked
        var blocked = ContractsSsrfGuard.IsBlocked("http://172.15.0.1/", out _);
        Assert.False(blocked, "172.15.0.1 is outside the 172.16.0.0/12 range and should be allowed");
    }

    [Fact]
    public void IsBlocked_192_168_1_1_ReturnsTrue()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("http://192.168.1.1/", out _);
        Assert.True(blocked);
    }

    // ── Link-local ────────────────────────────────────────────────────────────

    [Fact]
    public void IsBlocked_169_254_1_1_ReturnsTrue_LinkLocal()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("http://169.254.1.1/", out _);
        Assert.True(blocked);
    }

    // ── Public IPs — should NOT be blocked ────────────────────────────────────

    [Fact]
    public void IsBlocked_1_1_1_1_ReturnsFalse_PublicIP()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("http://1.1.1.1/", out _);
        Assert.False(blocked, "1.1.1.1 is a public IP and should not be blocked");
    }

    [Fact]
    public void IsBlocked_8_8_8_8_ReturnsFalse_PublicIP()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("http://8.8.8.8/", out _);
        Assert.False(blocked, "8.8.8.8 is a public IP and should not be blocked");
    }

    // ── Invalid/empty URLs ────────────────────────────────────────────────────

    [Fact]
    public void IsBlocked_InvalidUrl_ReturnsTrueWithInvalidUrlReason()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("not-a-valid-url", out var reason);

        Assert.True(blocked);
        Assert.Contains("InvalidUrl", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsBlocked_EmptyUrl_ReturnsTrueWithInvalidUrlReason()
    {
        var blocked = ContractsSsrfGuard.IsBlocked("", out var reason);

        Assert.True(blocked);
        Assert.Contains("InvalidUrl", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Core_Shims_Delegate_To_Contracts_Helper()
    {
        var contractsBlocked = ContractsSsrfGuard.IsBlocked("http://127.0.0.1/api", out var contractsReason);
        var coreBlocked = CoreSsrfGuard.IsBlocked("http://127.0.0.1/api", out var coreReason);

        Assert.Equal(contractsBlocked, coreBlocked);
        Assert.Equal(contractsReason, coreReason);
    }
}
