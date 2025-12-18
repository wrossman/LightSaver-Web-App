using System.Security.Cryptography;
using System.Text;
public class HmacHelper
{
    private readonly byte[] _serverKey;
    private readonly ILogger<HmacHelper> _logger;
    public HmacHelper(IConfiguration config, ILogger<HmacHelper> logger)
    {
        var rawKey = config.GetValue<string>("Hmac");

        if (string.IsNullOrWhiteSpace(rawKey))
            throw new InvalidOperationException();

        string serverKey = rawKey.Trim();

        _serverKey = Convert.FromBase64String(serverKey);
        _logger = logger;
    }
    public string Hash(string value)
    {
        if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(value));

        byte[] keyBytes = Encoding.UTF8.GetBytes(value);

        using var hmac = new HMACSHA256(_serverKey);
        byte[] hash = hmac.ComputeHash(keyBytes);

        return Convert.ToHexString(hash);
    }
    public bool Verify(string value, string stored)
    {
        var computed = Hash(value);

        try
        {
            var computedBytes = Convert.FromHexString(computed);
            var storedBytes = Convert.FromHexString(stored);
            return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
        }
        catch
        {
            _logger.LogWarning("Failed to verify hash.");
            return false;
        }
    }
}
