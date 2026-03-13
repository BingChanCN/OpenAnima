using System.Net;
using System.Net.Sockets;

namespace OpenAnima.Core.Http;

/// <summary>
/// Static SSRF protection utility that blocks private, loopback, and link-local IP addresses.
/// Must be called before any network request to prevent Server-Side Request Forgery attacks.
/// </summary>
public static class SsrfGuard
{
    /// <summary>
    /// Blocked IP network ranges: loopback (127/8), RFC1918 private (10/8, 172.16/12, 192.168/16),
    /// link-local (169.254/16), and IPv6 private/ULA ranges (fc00::/7, fe80::/10).
    /// </summary>
    private static readonly (IPAddress Network, int PrefixLength)[] BlockedRanges =
    {
        // IPv4 loopback
        (IPAddress.Parse("127.0.0.0"), 8),
        // RFC 1918 private ranges
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.168.0.0"), 16),
        // Link-local
        (IPAddress.Parse("169.254.0.0"), 16),
        // IPv6 ULA (fc00::/7)
        (IPAddress.Parse("fc00::"), 7),
        // IPv6 link-local (fe80::/10)
        (IPAddress.Parse("fe80::"), 10),
    };

    /// <summary>
    /// Determines whether the given URL targets a blocked private/loopback address.
    /// Returns true (blocked) if: the URL is invalid, resolves to a private/loopback IP,
    /// or DNS resolution fails. Returns false only for confirmed public IPs.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <param name="reason">Human-readable reason if blocked; empty string if allowed.</param>
    /// <returns>true if the URL should be blocked; false if it is safe to use.</returns>
    public static bool IsBlocked(string url, out string reason)
    {
        // Validate URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            reason = "InvalidUrl";
            return true;
        }

        // Explicit localhost check before IP parse (handles the hostname case without DNS)
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            reason = "SsrfBlocked:Loopback";
            return true;
        }

        // If host is a direct IP literal, check it immediately (no DNS needed)
        if (IPAddress.TryParse(uri.Host, out var directIp))
        {
            if (IsPrivateOrLoopback(directIp))
            {
                reason = $"SsrfBlocked:{directIp}";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        // Hostname — resolve via DNS and check all returned addresses
        try
        {
            var addresses = Dns.GetHostAddresses(uri.Host);
            foreach (var addr in addresses)
            {
                if (IsPrivateOrLoopback(addr))
                {
                    reason = $"SsrfBlocked:{addr}";
                    return true;
                }
            }

            reason = string.Empty;
            return false;
        }
        catch (SocketException)
        {
            reason = "DnsResolutionFailed";
            return true;
        }
    }

    /// <summary>
    /// Returns true if the IP address is loopback, link-local, or falls within a blocked private range.
    /// </summary>
    private static bool IsPrivateOrLoopback(IPAddress addr)
    {
        if (IPAddress.IsLoopback(addr))
            return true;

        if (addr.IsIPv6LinkLocal)
            return true;

        return Array.Exists(BlockedRanges, range => IsInRange(addr, range.Network, range.PrefixLength));
    }

    /// <summary>
    /// Checks whether <paramref name="addr"/> falls within the CIDR range defined by
    /// <paramref name="network"/> and <paramref name="prefixLength"/>.
    /// Handles both IPv4 and IPv6. Returns false for address family mismatches.
    /// </summary>
    private static bool IsInRange(IPAddress addr, IPAddress network, int prefixLength)
    {
        if (addr.AddressFamily != network.AddressFamily)
            return false;

        var addrBytes = addr.GetAddressBytes();
        var netBytes = network.GetAddressBytes();

        // Compare bit by bit up to prefixLength
        var fullBytes = prefixLength / 8;
        var remainderBits = prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (addrBytes[i] != netBytes[i])
                return false;
        }

        if (remainderBits > 0)
        {
            var mask = (byte)(0xFF << (8 - remainderBits));
            if ((addrBytes[fullBytes] & mask) != (netBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }
}
