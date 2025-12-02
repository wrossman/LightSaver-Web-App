using System.Security.Cryptography;
using System.Text;

public static class Pbkdf2Hasher
{
    // THANKS CHAT AND CLAUDE
    // Tune this to match your performance target.
    private const int IterationCount = 150_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public static string Hash(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        // Generate salt
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);

        // Derive key using the static API
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(value),
            salt: salt,
            iterations: IterationCount,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSizeBytes);

        // Encode parts
        string saltB64 = Convert.ToBase64String(salt);
        string hashB64 = Convert.ToBase64String(hash);

        // Format: pbkdf2_sha256$iterations$salt$hash
        return $"pbkdf2_sha256${IterationCount}${saltB64}${hashB64}";
    }

    public static bool Verify(string value, string stored)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (string.IsNullOrWhiteSpace(stored)) return false;

        // Split stored string
        var parts = stored.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return false;
        if (!string.Equals(parts[0], "pbkdf2_sha256", StringComparison.Ordinal)) return false;

        if (!int.TryParse(parts[1], out int iterations)) return false;

        byte[] salt;
        byte[] storedHash;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            storedHash = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        // Compute hash for verification
        byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(value),
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: storedHash.Length);

        // Constant-time comparison
        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }
}
