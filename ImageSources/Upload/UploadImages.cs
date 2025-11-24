using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
public class UploadImages
{
    private readonly ILogger<UserSessions> _logger;
    private readonly RokuSessionDbContext _rokuSessionDb;
    private readonly UserSessionDbContext _userSessionDb;
    private readonly GlobalImageStoreDbContext _resourceDbContext;
    public UploadImages(ILogger<UserSessions> logger, UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb, GlobalImageStoreDbContext resourceDbContext)
    {
        _logger = logger;
        _userSessionDb = userSessionDb;
        _rokuSessionDb = rokuSessionDb;
        _resourceDbContext = resourceDbContext;
    }
    public async Task<bool> UploadImageFlow(List<IFormFile> images, string sessionId)
    {
        var sessionCode = await _userSessionDb.Sessions
                            .Where(s => s.Id == sessionId)
                            .Select(s => s.SessionCode)
                            .FirstOrDefaultAsync();
        if (sessionCode is null)
        {
            _logger.LogWarning("Failed to locate user session with sessionId " + sessionId);
            return false;
        }
        var rokuId = await _rokuSessionDb.Sessions
                        .Where(s => s.SessionCode == sessionCode)
                        .Select(s => s.RokuId)
                        .FirstOrDefaultAsync();
        if (rokuId is null)
        {
            _logger.LogWarning("Failed to locate roku session with session code " + sessionCode);
            return false;
        }

        foreach (var item in images)
        {
            if (item.Length <= 0) continue;

            byte[] imgBytes;
            using (var ms = new MemoryStream())
            {
                await item.CopyToAsync(ms);
                imgBytes = ms.ToArray();
            }

            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var key = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            string hash = GlobalHelpers.ComputeHashFromBytes(imgBytes);
            hash = hash + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

            ImageShare share = new()
            {
                Id = hash,
                Key = key,
                SessionCode = sessionCode,
                ImageStream = imgBytes,
                CreatedOn = DateTime.UtcNow,
                FileType = "", // should i figure out how to get the filetype? it isnt really necessary for roku
                RokuId = rokuId
            };
            _resourceDbContext.Resources.Add(share);
            await _resourceDbContext.SaveChangesAsync();
        }

        UserSessions.CodesReadyForTransfer.Enqueue(sessionCode);
        return true;
    }
    public async Task ExpireCreds(string sessionCode)
    {
        //remove sessioncode reference from resources
        if (await GlobalStoreHelpers.ScrubSessionCode(_resourceDbContext, sessionCode))
            _logger.LogInformation($"Scrubbed Image Resources of session code {sessionCode}");
        else
            _logger.LogWarning($"Failed to scrub resources of session code {sessionCode}");

        // expire user and roku session associated with session code
        if (await GlobalHelpers.ExpireRokuSession(_rokuSessionDb, sessionCode))
            _logger.LogInformation("Set roku session for expiration due to resource package delivery.");
        else
            _logger.LogWarning("Failed to set expire for roku session after resource package delivery.");

        if (await GlobalHelpers.ExpireUserSession(_userSessionDb, sessionCode))
            _logger.LogInformation("Set user session for expiration due to resource package delivery.");
        else
            _logger.LogWarning("Failed to set expire for user session after resource package delivery.");

    }
}