using System.Security.Cryptography;
using System.Text;

namespace StudioCRM.Infrastructure.Persistence;

// Encrypts third-party credentials (Outlook OAuth tokens) at rest with AES-256-GCM.
// The key comes from Security:TokenEncryptionKey (base64, 32 bytes). Values written before the key was set
// stay readable as plaintext and are encrypted on their next save (tokens are refreshed regularly).
public static class TokenProtector
{
    private const string Prefix = "enc:v1:";
    private static byte[]? _key;

    public static bool IsConfigured => _key != null;

    public static void Configure(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            _key = null;
            return;
        }
        var key = Convert.FromBase64String(base64Key.Trim());
        if (key.Length != 32)
            throw new InvalidOperationException("Security:TokenEncryptionKey must be 32 bytes encoded as base64.");
        _key = key;
    }

    public static string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || _key == null || plaintext.StartsWith(Prefix, StringComparison.Ordinal))
            return plaintext ?? string.Empty;
        var data = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var cipher = new byte[data.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, data, cipher, tag);
        return Prefix + Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored ?? string.Empty;
        if (_key == null)
            throw new InvalidOperationException("Encrypted token found but Security:TokenEncryptionKey is not configured.");
        var bytes = Convert.FromBase64String(stored[Prefix.Length..]);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        var nonce = bytes[..nonceSize];
        var tag = bytes[nonceSize..(nonceSize + tagSize)];
        var cipher = bytes[(nonceSize + tagSize)..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, tagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
