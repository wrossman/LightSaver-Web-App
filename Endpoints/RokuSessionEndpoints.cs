using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Primitives;
public static class RokuSessionEndpoints
{
    public static void MapRokuSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/roku")
            .RequireRateLimiting("by-ip-policy");

        group.MapPost("/code", ProvideSessionCode);
        group.MapPost("/reception", RokuReception);
        group.MapPost("/resource-package", (Delegate)ProvideResourcePackage);
        group.MapGet("/get-resource", ProvideResource);
    }
    private static async Task<IResult> ProvideSessionCode(HttpContext context, RokuSessions roku, ILogger<RokuSessions> logger)
    {
        var request = context.Request;
        var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);

        var body = await RokuSessions.ReadRokuPost(context);
        if (body == "fail")
        {
            logger.LogWarning("An oversized payload was received at roku session code provider endpoint");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var jsonBody = JsonSerializer.Deserialize<RokuIdPostBody>(body);
        var rokuId = jsonBody?.RokuId;

        if (string.IsNullOrEmpty(rokuId))
        {
            logger.LogWarning("Roku Id was not found in roku session code provider endpoint connection.");
            return Results.BadRequest();
        }

        string sessionCode = await roku.CreateRokuSession(remoteIpAddress, rokuId);
        if (sessionCode == string.Empty)
        {
            logger.LogWarning($"Failed to create roku session for  {remoteIpAddress}");
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        return Results.Json(new { RokuSessionCode = sessionCode });
    }
    private static async Task<IResult> RokuReception(HttpContext context, RokuSessionDbContext rokuSessionDb, RokuSessions roku, ILogger<RokuSessions> logger)
    {    //be carefull about what i return to the user because they cant be able to see what is a valid session code

        //thanks copilot for helping me read the post request
        var body = await RokuSessions.ReadRokuPost(context);
        if (body == "fail")
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var jsonBody = JsonSerializer.Deserialize<RokuSessionPostBody>(body);
        var sessionCode = jsonBody?.RokuSessionCode;

        if (string.IsNullOrEmpty(sessionCode))
        {
            logger.LogWarning("An invalid SessionCode was tried at /google/roku-google-reception endpoint");
            return Results.NotFound("Media is not ready to be transfered.");
        }

        if (await roku.CheckReadyTransfer(sessionCode))
        {
            return Results.Content("Ready");
        }
        else
        {
            return Results.NotFound("Media is not ready to be transfered.");
        }
    }
    private static async Task<IResult> ProvideResourcePackage(HttpContext context, GlobalImageStoreDbContext resourceDbContext, ILogger<RokuSessions> logger, UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb)
    {
        var body = await RokuSessions.ReadRokuPost(context);
        if (body == "fail")
        {
            logger.LogWarning($"IP: {context.Connection.RemoteIpAddress} failed to retrieve resource package. Request payload was too large.");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var jsonBody = JsonSerializer.Deserialize<RokuSessionIdPostBody>(body);
        var sessionCode = jsonBody?.RokuSessionCode;
        var rokuId = jsonBody?.RokuId;

        if (rokuId is null || sessionCode is null)
        {
            logger.LogWarning($"IP: {context.Connection.RemoteIpAddress} failed to retrieve resource package. Provided RokuId or SessionCode was invalid.");
            return Results.BadRequest("Failed to retrieve resource package.");
        }

        var links = GlobalStoreHelpers.GetResourcePackage(resourceDbContext, sessionCode, rokuId);

        if (links is null || links.Count == 0)
        {
            logger.LogWarning($"IP: {context.Connection.RemoteIpAddress} failed to retrieve resource package. Provided RokuId or SessionCode was invalid.");
            return Results.BadRequest("Failed to retrieve resource package.");
        }

        //remove sessioncode reference from resources
        if (await GlobalStoreHelpers.ScrubSessionCode(resourceDbContext, sessionCode))
            logger.LogInformation($"Scrubbed Image Resources of session code {sessionCode}");
        else
            logger.LogWarning($"Failed to scrub resources of session code {sessionCode}");

        // expire user and roku session associated with session code
        if (await GlobalHelpers.ExpireRokuSession(rokuSessionDb, sessionCode))
            logger.LogInformation("Set roku session for expiration due to resource package delivery.");
        else
            logger.LogWarning("Failed to set expire for roku session after resource package delivery.");

        if (await GlobalHelpers.ExpireUserSession(userSessionDb, sessionCode))
            logger.LogInformation("Set user session for expiration due to resource package delivery.");
        else
            logger.LogWarning("Failed to set expire for user session after resource package delivery.");

        logger.LogInformation($"Sending resource package for session code {sessionCode} to IP: {context.Connection.RemoteIpAddress}");
        return Results.Json(links);
    }
    private static IResult ProvideResource(HttpContext context, GlobalImageStoreDbContext resourceDbContext, ILogger<RokuSessions> logger)
    {
        StringValues key;
        StringValues location;
        StringValues device;
        if (!context.Request.Headers.TryGetValue("Authorization", out key))
            return Results.Unauthorized();
        if (!context.Request.Headers.TryGetValue("Location", out location))
            return Results.Unauthorized();
        if (!context.Request.Headers.TryGetValue("Device", out device))
            return Results.Unauthorized();

        logger.LogInformation($"Received key: {key} for file {location} from device {device}");

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(device) || string.IsNullOrEmpty(location))
            return Results.Unauthorized();

        (byte[]? image, string? fileType) = GlobalStoreHelpers.GetResourceData(resourceDbContext, location.ToString(), key.ToString(), device.ToString());

        if (image is null || fileType is null)
        {
            logger.LogInformation("Old or incorrect keys were tried against the database.");
            return Results.Unauthorized();
        }

        // For testing image output - this thing is dangerous, dont let it run for a long time because it writes to desktop unless you stop it
        GlobalStoreHelpers.WritePhotosToLocal(image, fileType);

        return Results.File(image, $"image/{fileType}");
    }

}