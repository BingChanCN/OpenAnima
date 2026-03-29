using System.Security.Cryptography;
using OpenAnima.Core.Providers;

namespace OpenAnima.Tests.Unit;

public class ApiKeyProtectorTests
{
    private static readonly byte[] _testKey = ApiKeyProtector.DeriveKey("test-machine:test-user:/test/path/");

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginalPlaintext()
    {
        const string original = "sk-test-1234567890";

        var encrypted = ApiKeyProtector.Encrypt(original, _testKey);
        var decrypted = ApiKeyProtector.Decrypt(encrypted, _testKey);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertext_OnEachCall()
    {
        const string plaintext = "sk-test-1234567890";

        var cipher1 = ApiKeyProtector.Encrypt(plaintext, _testKey);
        var cipher2 = ApiKeyProtector.Encrypt(plaintext, _testKey);

        // Random nonce means ciphertext differs each call
        Assert.NotEqual(cipher1, cipher2);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsCryptographicException()
    {
        const string plaintext = "sk-test-1234567890";
        var encrypted = ApiKeyProtector.Encrypt(plaintext, _testKey);
        var wrongKey = ApiKeyProtector.DeriveKey("wrong-machine:wrong-user:/wrong/path/");

        // AuthenticationTagMismatchException inherits from CryptographicException
        Assert.ThrowsAny<CryptographicException>(() => ApiKeyProtector.Decrypt(encrypted, wrongKey));
    }

    [Fact]
    public void MaskForDisplay_EmptyString_ReturnsEmptyString()
    {
        var result = ApiKeyProtector.MaskForDisplay(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void MaskForDisplay_NonEmptyEncryptedValue_ReturnsExpectedPattern()
    {
        const string plaintext = "sk-test-1234567890";
        var encrypted = ApiKeyProtector.Encrypt(plaintext, _testKey);

        var masked = ApiKeyProtector.MaskForDisplay(encrypted);

        // Should start with sk-**** and end with last 4 chars of ciphertext
        Assert.StartsWith("sk-****...", masked);
        Assert.Equal(encrypted[^4..], masked[^4..]);
    }

    [Fact]
    public void DeriveKey_ProducesConsistent32ByteOutput_ForSameInput()
    {
        const string fingerprint = "test-machine:test-user:/app/path/";

        var key1 = ApiKeyProtector.DeriveKey(fingerprint);
        var key2 = ApiKeyProtector.DeriveKey(fingerprint);

        Assert.Equal(32, key1.Length);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void DeriveKey_ProducesDifferentOutput_ForDifferentInput()
    {
        var key1 = ApiKeyProtector.DeriveKey("machine-a:user-a:/path-a/");
        var key2 = ApiKeyProtector.DeriveKey("machine-b:user-b:/path-b/");

        Assert.NotEqual(key1, key2);
    }
}
