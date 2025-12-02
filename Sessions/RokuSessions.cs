using System.Security.Cryptography;
using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text;
public class RokuSessions
{
    private readonly ILogger<RokuSessions> _logger;
    private readonly RokuSessionDbContext _rokuSessionDb;
    public RokuSessions(ILogger<RokuSessions> logger, RokuSessionDbContext rokuSessionDb)
    {
        _logger = logger;
        _rokuSessionDb = rokuSessionDb;
    }
    public async Task<RokuSession> CreateRokuSession(IPAddress ipAddress, string rokuId, int maxScreenSize)
    {
        // remove session if one exists with provided rokuID
        var item = await _rokuSessionDb.RokuSessions
        .FirstOrDefaultAsync(x => x.RokuId == rokuId);

        if (item != null)
        {
            _rokuSessionDb.RokuSessions.Remove(item);
            await _rokuSessionDb.SaveChangesAsync();
        }

        bool retry;
        RokuSession session = new();
        do
        {
            retry = false;
            try
            {
                session = new()
                {
                    Id = await GenerateSessionId(),
                    RokuId = rokuId,
                    CreatedAt = DateTime.UtcNow,
                    SourceAddress = ipAddress.ToString(),
                    SessionCode = GenerateSessionCode(),
                    ReadyForTransfer = false,
                    Expired = false,
                    MaxScreenSize = maxScreenSize
                };

                _rokuSessionDb.Add(session);
                await _rokuSessionDb.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                _logger.LogInformation($"A roku session could not be created due to a SessionCode collision: {e.Message}");
                retry = true;
            }
        } while (retry);

        return session;
    }
    private async Task<string> GenerateSessionId()
    {
        using var rng = RandomNumberGenerator.Create();
        string rokuSessionId;
        RokuSession? session;
        //thanks copilot ish
        do
        {
            // Generate 16 random bytes (128 bits)
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            // Convert to hex string (32 hex chars)
            rokuSessionId = Convert.ToHexString(bytes); // e.g., "A1B2C3D4..."
            session = await _rokuSessionDb.RokuSessions
            .FirstOrDefaultAsync(x => x.Id == rokuSessionId);

        } while (session is not null);

        return rokuSessionId;
    }
    private static string GenerateSessionCode()
    {
        // thanks chat
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        using var rng = RandomNumberGenerator.Create();

        var bytes = new byte[7];
        rng.GetBytes(bytes);

        char[] result = new char[7];

        for (int i = 0; i < 7; i++)
        {
            // Convert random byte (0–255) into index (0–35)
            result[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(result);
    }
    public async Task<RokuSession?> GetRokuSession(string sessionCode, string rokuId)
    {
        return await _rokuSessionDb.RokuSessions
                        .FirstOrDefaultAsync(s => s.SessionCode == sessionCode &&
                         s.RokuId == rokuId);
    }
    public static async Task<string> ReadRokuPost(HttpContext context)
    {
        const int maxBytes = 512;
        var buffer = new byte[32];
        int totalBytes = 0;

        using var memoryStream = new MemoryStream();
        int bytesRead;

        do
        {
            bytesRead = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            totalBytes += bytesRead;

            if (totalBytes > maxBytes)
                return "fail";

            await memoryStream.WriteAsync(buffer, 0, bytesRead);
        }
        while (bytesRead > 0);

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}