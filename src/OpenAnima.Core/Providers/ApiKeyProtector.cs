using System.Security.Cryptography;
using System.Text;

namespace OpenAnima.Core.Providers;

/// <summary>
/// Static helper for AES-GCM encryption and decryption of LLM provider API keys.
/// Uses PBKDF2 key derivation from machine fingerprint. Keys are stored as
/// "base64nonce:base64tag:base64ciphertext" — never as plaintext.
///
/// SECURITY CONTRACT: No method in this class ever passes a decrypted key value
/// to any logging framework or returns it in a context where it could be observed
/// from outside an authenticated in-memory call scope.
/// </summary>
public static class ApiKeyProtector
{
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("OpenAnima.ProviderRegistry.v1");

    /// <summary>
    /// Derives a 256-bit AES key from the machine fingerprint using PBKDF2/SHA-256.
    /// Deterministic: same fingerprint always produces the same key.
    /// </summary>
    public static byte[] DeriveKey(string machineFingerprint)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            machineFingerprint, Salt, iterations: 100_000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }

    /// <summary>
    /// Returns a stable fingerprint derived from the current machine's identity.
    /// Combines machine name, user name, and application base directory.
    /// </summary>
    public static string GetMachineFingerprint()
        => $"{Environment.MachineName}:{Environment.UserName}:{AppContext.BaseDirectory}";

    /// <summary>
    /// Encrypts plaintext using AES-GCM with a random 12-byte nonce.
    /// Returns "base64nonce:base64tag:base64ciphertext".
    /// Each call produces different ciphertext due to random nonce.
    /// </summary>
    public static string Encrypt(string plaintext, byte[] key)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];     // 16 bytes
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return $"{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(cipherBytes)}";
    }

    /// <summary>
    /// Decrypts a value produced by Encrypt. Throws CryptographicException if
    /// the key is wrong or the ciphertext has been tampered with (authenticated encryption).
    /// </summary>
    public static string Decrypt(string encryptedValue, byte[] key)
    {
        var parts = encryptedValue.Split(':');
        if (parts.Length != 3)
            throw new CryptographicException("Invalid encrypted value format. Expected 'nonce:tag:ciphertext'.");

        var nonce = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var cipherBytes = Convert.FromBase64String(parts[2]);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Returns a safe display string for a stored encrypted value.
    /// Empty input returns empty string. Non-empty returns "sk-****...{last4}"
    /// where last4 is the final 4 characters of the ciphertext blob — not the plaintext key.
    /// This satisfies PROV-07: write-only display that confirms a key exists without revealing it.
    /// </summary>
    public static string MaskForDisplay(string encryptedValue)
    {
        if (string.IsNullOrEmpty(encryptedValue))
            return string.Empty;

        var suffix = encryptedValue.Length >= 4
            ? encryptedValue[^4..]
            : encryptedValue;

        return $"sk-****...{suffix}";
    }
}
