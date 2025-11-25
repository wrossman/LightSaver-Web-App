using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Mvc;
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
        group.MapGet("/initial", InitialStartWallpaper);
        group.MapPost("/revoke", RevokeAccess);
    }
    private static async Task<IResult> ProvideSessionCode(HttpContext context, RokuSessions roku, ILogger<RokuSessions> logger)
    {

        // should i use middleware to manage size restriction for post bodies and then include the json body model in the signature
        var body = await RokuSessions.ReadRokuPost(context);
        if (body == "fail")
        {
            logger.LogWarning("An oversized payload was received at roku session code provider endpoint");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var remoteIpAddress = context.Request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);
        var jsonBody = JsonSerializer.Deserialize<RokuProvideSessionCodePostBody>(body);
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
    private static async Task<IResult> RokuReception(HttpContext context, RokuSessions roku, ILogger<RokuSessions> logger)
    {    //be carefull about what i return to the user because they cant be able to see what is a valid session code

        //thanks copilot for helping me read the post request
        var body = await RokuSessions.ReadRokuPost(context);
        if (body == "fail")
        {
            logger.LogWarning("An oversized payload was received at roku session code provider endpoint");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var jsonBody = JsonSerializer.Deserialize<RokuReceptionPostBody>(body);
        var sessionCode = jsonBody?.SessionCode;
        var rokuId = jsonBody?.RokuId;

        if (string.IsNullOrEmpty(sessionCode))
        {
            logger.LogWarning("An invalid SessionCode was tried at roku reception endpoint");
            return Results.NotFound("Media is not ready to be transfered.");
        }
        if (string.IsNullOrEmpty(rokuId))
        {
            logger.LogWarning("An invalid rokuId was tried at roku reception endpoint");
            return Results.NotFound("Media is not ready to be transfered.");
        }

        if (await roku.CheckReadyTransfer(sessionCode, rokuId))
            return Results.Content("Ready");
        else
            return Results.NotFound("Media is not ready to be transfered.");
    }
    private static async Task<IResult> ProvideResourcePackage(HttpContext context, SessionHelpers sessions, GlobalStoreHelpers store, ILogger<RokuSessions> logger)
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

        var links = store.GetResourcePackage(sessionCode, rokuId);

        if (links is null || links.Count == 0)
        {
            logger.LogWarning($"IP: {context.Connection.RemoteIpAddress} failed to retrieve resource package. Provided RokuId or SessionCode was invalid.");
            return Results.BadRequest("Failed to retrieve resource package.");
        }

        //remove sessioncode reference from resources
        if (await store.ScrubSessionCode(sessionCode))
            logger.LogInformation($"Scrubbed Image Resources of session code {sessionCode}");
        else
            logger.LogWarning($"Failed to scrub resources of session code {sessionCode}");

        // expire user and roku session associated with session code
        if (await sessions.ExpireRokuSession(sessionCode))
            logger.LogInformation("Set roku session for expiration due to resource package delivery.");
        else
            logger.LogWarning("Failed to set expire for roku session after resource package delivery.");

        if (await sessions.ExpireUserSession(sessionCode))
            logger.LogInformation("Set user session for expiration due to resource package delivery.");
        else
            logger.LogWarning("Failed to set expire for user session after resource package delivery.");

        logger.LogInformation($"Sending resource package for session code {sessionCode} to IP: {context.Connection.RemoteIpAddress}");
        return Results.Json(links);
    }
    private static IResult ProvideResource(HttpContext context, GlobalStoreHelpers store, ILogger<RokuSessions> logger)
    {
        StringValues inputKey;
        StringValues inputLocation;
        StringValues inputDevice;
        if (!context.Request.Headers.TryGetValue("Authorization", out inputKey))
            return Results.Unauthorized();
        if (!context.Request.Headers.TryGetValue("Location", out inputLocation))
            return Results.Unauthorized();
        if (!context.Request.Headers.TryGetValue("Device", out inputDevice))
            return Results.Unauthorized();

        string? key = inputKey;
        string? location = inputLocation;
        string? device = inputDevice;

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(device) || string.IsNullOrEmpty(location))
            return Results.Unauthorized();

        logger.LogInformation($"Received key: {key} for file: {location} from device: {device}");

        (byte[]? image, string? fileType) = store.GetResourceData(location.ToString(), key.ToString(), device.ToString());

        if (image is null || fileType is null)
        {
            logger.LogInformation("Old or incorrect keys were tried against the database.");
            return Results.Unauthorized();
        }

        // For testing image output - this thing is dangerous, dont let it run for a long time because it writes to desktop unless you stop it
        // store.WritePhotosToLocal(image, fileType);

        return Results.File(image, $"image/{fileType}");
    }
    public static async Task<IResult> InitialStartWallpaper(HttpContext context, GlobalStoreHelpers store, LightroomService lightroom, ILogger<RokuSessions> logger)
    {
        StringValues inputKey;
        StringValues inputLocation;
        StringValues inputDevice;
        if (!context.Request.Headers.TryGetValue("Authorization", out inputKey))
            return Results.Unauthorized();
        if (!context.Request.Headers.TryGetValue("Location", out inputLocation))
            return Results.Unauthorized();
        if (!context.Request.Headers.TryGetValue("Device", out inputDevice))
            return Results.Unauthorized();

        string? key = inputKey;
        string? location = inputLocation;
        string? device = inputDevice;

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(device) || string.IsNullOrEmpty(location))
            return Results.Unauthorized();

        logger.LogInformation($"Initial connection with key: {key} for file: {location} from device: {device}");

        if (!(store.GetResourceSource(location, key, device) == "lightroom"))
            return Results.Ok();

        var newPackage = await lightroom.UpdateRokuLinks(location, key, device);
        if (newPackage is null)
            return Results.Ok();

        return Results.Json(newPackage);
    }
    private static async Task<IResult> RevokeAccess([FromBody] RevokeAccessPackage revokePackage, GlobalStoreHelpers store, ILogger<RokuSessions> logger)
    {
        logger.LogInformation("Received the following data from roku:");
        string receiveLog = "";
        foreach (var item in revokePackage.Links)
        {
            receiveLog += item.Key;
            receiveLog += "\n";
            receiveLog += item.Value;
            receiveLog += "\n";
        }
        logger.LogInformation(receiveLog);

        var failedRevoke = await store.RevokeResourcePackage(revokePackage);

        if (failedRevoke.Links.Count > 0)
        {
            logger.LogWarning("Failed to remove all images from RevokePackage\nThe following resources were not removed");
            string failedToRemove = "";
            foreach (var item in failedRevoke.Links)
            {
                failedToRemove += item.Key + "\n";
                failedToRemove += item.Value + "\n";
            }
            logger.LogWarning(failedToRemove);
        }

        return Results.Ok();
    }
}