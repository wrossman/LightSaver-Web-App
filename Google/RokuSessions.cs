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
    public async Task<string> CreateRokuSession(IPAddress ipAddress, string rokuId)
    {
        // remove session if one exists with provided rokuID
        var item = await _rokuSessionDb.Sessions
        .FirstOrDefaultAsync(x => x.RokuId == rokuId);

        if (item != null)
        {
            _rokuSessionDb.Sessions.Remove(item);
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
                    Id = 0,
                    RokuId = rokuId,
                    CreatedAt = DateTime.UtcNow,
                    SourceAddress = ipAddress.ToString(),
                    SessionCode = GenerateSessionCode(),
                    ReadyForTransfer = false,
                    Expired = false
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

        string rokuSessionLog = "";
        rokuSessionLog += "Finished saving the following roku session to rokuSession database:\n";
        foreach (PropertyInfo prop in session.GetType().GetProperties())
        {
            var name = prop.Name;
            var value = prop.GetValue(session, null);
            rokuSessionLog += $"{name} = {value}\n";
        }
        _logger.LogInformation(rokuSessionLog);

        return session.SessionCode;
    }

    private static string GenerateSessionCode()
    {
        using var rng = RandomNumberGenerator.Create();
        string sessionCode = "";

        var bytes = new byte[4];
        rng.GetBytes(bytes);

        int value = BitConverter.ToInt32(bytes, 0) & int.MaxValue; // ensure non-negative
        int code = value % 0x1000000; // restrict to 6 hex digits (0x000000 - 0xFFFFFF), thanks copilot
        sessionCode = code.ToString("X6");

        return sessionCode;
    }

    public async Task<bool> CheckReadyTransfer(string sessionCode)
    {
        var rokuSession = await _rokuSessionDb.Sessions
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        if (rokuSession != null && rokuSession.ReadyForTransfer == true)
            return true;
        else
            return false;
    }

    public static async Task<string> ReadRokuPost(HttpContext context)
    {
        context.Request.EnableBuffering(); // allows re-reading the stream

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