using System.Security.Cryptography;
using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Net.NetworkInformation;
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
        // TODO: also check if we already have a roku session with this device id.
        if (!await CheckIpSessionCount(ipAddress))
            return string.Empty;
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
                    ReadyForTransfer = false
                };

                // write usersession to database and write sessioncode to hashset
                _rokuSessionDb.Add(session);
                // add check to ensure that the data was written
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
    private async Task<bool> CheckIpSessionCount(IPAddress ipAddress)
    {
        string ipAddressStr = ipAddress.ToString();
        _logger.LogInformation($"Roku device {ipAddressStr} just tried to connect");

        int result = await _rokuSessionDb.Sessions.CountAsync(s => s.SourceAddress == ipAddressStr);

        if (result <= 3)
            return true;
        else
            return false;

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