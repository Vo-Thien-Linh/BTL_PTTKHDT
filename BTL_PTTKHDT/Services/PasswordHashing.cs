using System.Security.Cryptography;
using System.Text;

namespace BTL_PTTKHDT.Services;

public static class PasswordHashing
{
    public static string Hash(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        var normalizedHash = storedHash.Trim();
        if (string.Equals(password, normalizedHash, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(Hash(password), normalizedHash, StringComparison.OrdinalIgnoreCase);
    }
}
