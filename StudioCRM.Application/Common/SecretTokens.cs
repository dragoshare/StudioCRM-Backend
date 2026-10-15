using System.Security.Cryptography;
using System.Text;

namespace StudioCRM.Application.Common;

// Bearer-style secrets (refresh and password-reset tokens) are stored only as SHA-256 hashes,
// so reading the database does not give working session or reset links.
public static class SecretTokens
{
    public static string Hash(string? token) =>
        string.IsNullOrEmpty(token)
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    // A presented value in hash format is never accepted as a legacy plaintext token,
    // otherwise a leaked hash could be replayed directly.
    public static bool CanBeLegacyPlaintext(string? token) =>
        !string.IsNullOrEmpty(token) &&
        !(token.Length == 64 && token.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
}
