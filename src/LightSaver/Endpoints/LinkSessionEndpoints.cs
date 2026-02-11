using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Mvc;
using System.Security.Authentication;
public static class LinkSessionEndpoints
{
    public static void MapLinkSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/link")
            .RequireRateLimiting("by-ip-policy");

        group.MapPost("/code", ProvideSessionCode);
        group.MapPost("/reception", RokuReception);
        group.MapPost("/resource-package", (Delegate)ProvideResourcePackage);
        group.MapGet("/get-resource", ProvideResource);
        group.MapGet("/initial", InitialStartWallpaper);
        group.MapPost("/revoke", RevokeAccess);
        group.MapGet("/background", ProvideBackground);
        group.MapPost("/update", PollUpdateLightroom);
        group.MapGet("/session", CodeSubmissionPageUpload);
        group.MapPost("/source", SelectSource);

    }
    private static async Task<IResult> ProvideSessionCode([FromBody] RokuProvideSessionCodePostBody body, HttpContext context, LinkSessions linkSessions, ILogger<LinkSessions> logger)
    {
        // TODO: set endpoint post size limits

        logger.LogInformation("Client reached provide session code.");

        var remoteIpAddress = context.Request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);

        if (string.IsNullOrWhiteSpace(body.RokuId) || body.ScreenWidth <= 0 || body.ScreenHeight <= 0)
            return Results.BadRequest("Invalid request body.");

        var rokuId = body.RokuId;
        var screenWidth = body.ScreenWidth;
        var screenHeight = body.ScreenHeight;

        var sessionId = linkSessions.CreateLinkSession(remoteIpAddress, rokuId, screenWidth, screenHeight);
        string sessionCode;
        try
        {
            sessionCode = linkSessions.GetSessionCodeFromSession(sessionId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get session from created session Id");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        logger.LogInformation("Providing session code.");

        return Results.Json(new { LinkSessionCode = sessionCode, LinkSessionId = sessionId });
    }
    private static async Task<IResult> RokuReception([FromBody] RokuReceptionPostBody body, HttpContext context, LinkSessions linkSessions, ILogger<LinkSessions> logger)
    {
        // TODO: set endpoint post size limits
        if (string.IsNullOrWhiteSpace(body.RokuId) || string.IsNullOrEmpty(body.SessionCode) || body.SessionId == Guid.Empty)
            return Results.BadRequest();

        var sessionId = body.SessionId;
        var rokuId = body.RokuId;
        var sessionCode = body.SessionCode;

        if (sessionId == Guid.Empty || string.IsNullOrEmpty(rokuId) || string.IsNullOrEmpty(sessionCode))
        {
            logger.LogWarning("An invalid SessionCode was tried at roku reception endpoint");
            return Results.BadRequest();
        }

        // i should probably just have a single method that verifies the id, deviceid and session code and early exits
        // instead of doing it at each check method

        // this will early exit and return expired to any failed session id / deviceid / and session code check
        if (linkSessions.CheckExpired(sessionId, rokuId, sessionCode))
            return Results.Content("Expired");

        if (linkSessions.CheckReadyForTransfer(sessionId, rokuId, sessionCode))
            return Results.Content("Ready");

        return Results.Content(linkSessions.GetDownloadedResourceCount(sessionId).ToString());
    }
    private static async Task<IResult> ProvideResourcePackage([FromBody] RokuReceptionPostBody body, HttpContext context, LinkSessions linkSessions, GlobalStore store, ILogger<LinkSessions> logger)
    {

        if (string.IsNullOrWhiteSpace(body.RokuId) || string.IsNullOrEmpty(body.SessionCode) || body.SessionId == Guid.Empty)
            return Results.BadRequest("Invalid request body.");

        var sessionId = body.SessionId;
        var rokuId = body.RokuId;
        var sessionCode = body.SessionCode;

        if (sessionId == Guid.Empty || string.IsNullOrEmpty(rokuId) || string.IsNullOrEmpty(sessionCode))
        {
            logger.LogWarning("Invalid input was detected at the Provide Resource Endpoint.");
            return Results.NotFound("Failed to retrieve resource package.");
        }

        Dictionary<Guid, string> links;
        try
        {
            links = linkSessions.GetResourcePackage(sessionId, sessionCode, rokuId);
        }
        catch (AuthenticationException e)
        {
            logger.LogError(e, "User tried to access a session with incorrect credentials.");
            return Results.Unauthorized();
        }

        if (links.Count == 0)
        {
            logger.LogWarning($"User tried to retrieve an empty resource package.");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        //remove images from resource store that are from this roku but from old sessions
        // this protects from storing old images that the device cant access
        if (await store.ScrubOldImages(rokuId, sessionCode))
            logger.LogInformation($"Scrubbed Image Resources of session id {sessionId}");
        else
            logger.LogWarning($"Failed to scrub resources of session id {sessionId}");

        if (linkSessions.ExpireSession(sessionId))
            logger.LogInformation($"Set roku and user session with session id {sessionId} for expiration due to resource package delivery.");
        else
            logger.LogWarning($"Failed to set expire for user and roku session with session id {sessionId} after resource package delivery.");

        logger.LogInformation($"Sending resource package for session id {sessionId}");
        return Results.Json(links);
    }
    private static async Task<IResult> ProvideResource(HttpContext context, GlobalStore store, ILogger<LinkSessions> logger)
    {
        StringValues inputKey;
        StringValues inputResourceId;
        StringValues inputDevice;
        if (!context.Request.Headers.TryGetValue("Authorization", out inputKey)
        || !context.Request.Headers.TryGetValue("ResourceId", out inputResourceId)
        || !context.Request.Headers.TryGetValue("Device", out inputDevice))
        {
            logger.LogWarning("Failed to TryGet input data");
            return Results.Unauthorized();
        }

        string? key = inputKey;
        Guid resourceId;
        string? device = inputDevice;

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(device) || !Guid.TryParse(inputResourceId, out resourceId))
            return Results.Unauthorized();

        byte[] resource;
        try
        {
            var updateKey = await store.GetUpdatedKey(resourceId, key, device);
            if (updateKey is not null)
            {
                logger.LogInformation($"Returning update key for resource id {resourceId.ToString()}");
                return Results.Json(new { Key = updateKey }, statusCode: StatusCodes.Status202Accepted);
            }
            resource = await store.GetResourceData(resourceId, key, device);
        }
        catch (ArgumentException e)
        {
            logger.LogError(e, "Client tried an invalid resource Id at the Provide Resource Endpoint.");
            return Results.Unauthorized();
        }
        catch (AuthenticationException e)
        {
            logger.LogError(e, "Client tried an invalid key at the Provide Resource Endpoint");
            return Results.Unauthorized();
        }
        catch (IOException)
        {
            logger.LogWarning("User tried to access a file that does not exist any more.");
            return Results.Unauthorized();
        }

        return Results.File(resource, $"image/webp");
    }
    public static async Task<IResult> InitialStartWallpaper(IConfiguration config, HttpContext context, GlobalStore store, LightroomService lightroom, ILogger<LinkSessions> logger)
    {
        StringValues inputKey;
        StringValues inputResourceId;
        StringValues inputDevice;
        StringValues inputScreenWidth;
        StringValues inputScreenHeight;
        if (!context.Request.Headers.TryGetValue("Authorization", out inputKey)
        || !context.Request.Headers.TryGetValue("ResourceId", out inputResourceId)
        || !context.Request.Headers.TryGetValue("Device", out inputDevice)
        || !context.Request.Headers.TryGetValue("MaxScreenSize", out inputScreenWidth)
        || !context.Request.Headers.TryGetValue("MaxScreenSize", out inputScreenHeight))
            return Results.Unauthorized();

        string? key = inputKey;
        Guid resourceId;
        string? rokuId = inputDevice;
        int screenWidth;
        int screenHeight;

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(rokuId) || !Guid.TryParse(inputResourceId, out resourceId) || !Int32.TryParse(inputScreenWidth, out screenWidth) || !Int32.TryParse(inputScreenHeight, out screenHeight))
            return Results.Unauthorized();

        logger.LogInformation($"Received Initial request from {context.Connection.RemoteIpAddress}");

        var resourceReq = new ResourceRequest(resourceId, key, rokuId, screenWidth, screenHeight);

        try
        {
            if (!(store.GetResourceSource(resourceReq) == ImageShareSource.Lightroom))
                return Results.Ok();
        }
        catch (AuthenticationException)
        {
            logger.LogWarning("Client tried invalid resource request to initial get endpoint.");
            return Results.Unauthorized();
        }

        (Guid, string)? sessionResult;
        try
        {
            sessionResult = await lightroom.UpdateRokuLinks(resourceReq);
        }
        catch (InvalidOperationException)
        {
            logger.LogWarning("Roku device tried to update a lightroom album but it had more than the max files allowed.");
            return Results.Json(new { maxImages = config.GetValue<int>("MaxImages").ToString() });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error occurred while getting updated roku links.");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (sessionResult is null)
            return Results.Ok();

        logger.LogInformation($"Providing session key to client for album update.");

        var (sessionId, sessionKey) = sessionResult.Value;

        return Results.Json(new { SessionId = sessionId, SessionKey = sessionKey });
    }
    private static async Task<IResult> RevokeAccess([FromBody] RevokeAccessPackage revokePackage, GlobalStore store, ILogger<LinkSessions> logger)
    {
        logger.LogInformation($"Received revoke access package from roku with {revokePackage.Links.Count} resources.");

        var failedRevoke = await store.RevokeResourcePackage(revokePackage);

        if (failedRevoke.Links.Count > 0)
            logger.LogWarning($"Failed to remove {failedRevoke.Links.Count} images from revoke package.");
        else
            logger.LogInformation($"Removed {revokePackage.Links.Count} images from resource database.");

        return Results.Ok();
    }
    private static async Task<IResult> ProvideBackground(HttpContext context, GlobalStore store, ILogger<LinkSessions> logger)
    {
        StringValues inputKey;
        StringValues inputResourceId;
        StringValues inputDevice;
        if (!context.Request.Headers.TryGetValue("Authorization", out inputKey)
        || !context.Request.Headers.TryGetValue("ResourceId", out inputResourceId)
        || !context.Request.Headers.TryGetValue("Device", out inputDevice))
            return Results.Unauthorized();

        string? key = inputKey;
        Guid resourceId;
        string? device = inputDevice;

        if (string.IsNullOrEmpty(key) ||
        string.IsNullOrEmpty(device) ||
        !Guid.TryParse(inputResourceId, out resourceId))
        {
            return Results.Unauthorized();
        }

        byte[]? image = await store.GetResourceData(resourceId, key, device, true);

        if (image is null)
        {
            return Results.Empty;
        }

        return Results.File(image, "image/webp");
    }
    public static async Task<IResult> PollUpdateLightroom([FromBody] RokuUpdateLightroomPostBody body, HttpContext context, ILogger<LightroomUpdateSessions> logger, LightroomUpdateSessions updateSessions)
    {

        if (string.IsNullOrWhiteSpace(body.RokuId) || string.IsNullOrEmpty(body.Key) || body.Id == Guid.Empty)
            return Results.BadRequest("Invalid request body.");

        Guid id = body.Id;
        var key = body.Key;
        var rokuId = body.RokuId;

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(rokuId) || id == Guid.Empty)
        {
            logger.LogWarning("Invalid body was posted to lightroom update polling endpoint.");
            return Results.Unauthorized();
        }

        try
        {
            if (updateSessions.CheckReadyForTransfer(id, rokuId, key))
            {
                var resourcePackage = updateSessions.GetResourcePackage(id, key, rokuId);
                updateSessions.ExpireSession(id);
                return Results.Json(resourcePackage);
            }
            else
            {
                logger.LogInformation("Check ready for transfer returned false");
                return Results.Content("Media is not ready to be transferred.");
            }
        }
        catch (AuthenticationException)
        {
            logger.LogError("User tried invalid keys at Poll Update Lightroom endpoint.");
            return Results.Unauthorized();
        }
    }
    private static IResult CodeSubmissionPageUpload(IWebHostEnvironment env, HttpContext context)
    {
        // append test cookie to response, read it at code submission page and redirect if cookies aren't allowed
        context.Response.Cookies.Append("AllowCookie", "LightSaver", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
        return Results.File(env.WebRootPath + "/EnterSessionCode.html", "text/html");
    }
    private async static Task<IResult> SelectSource(IWebHostEnvironment env, LinkSessions linkSessions, HttpContext context, ILogger<LinkSessions> logger)
    {
        // try get test cookie
        if (!context.Request.Cookies.TryGetValue("AllowCookie", out _))
            return GlobalHelpers.CreateErrorPage("Photo selection failed. LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");

        var rokuCodeForm = await context.Request.ReadFormAsync();
        if (rokuCodeForm is null)
            return Results.BadRequest();

        string? sessionCode = rokuCodeForm["code"];
        if (sessionCode is null)
            return Results.BadRequest();

        Guid sessionId = linkSessions.GetSessionCodeSession(sessionCode);
        if (sessionId == Guid.Empty)
        {
            return GlobalHelpers.CreateErrorPage("Unable to find session.", "<a href=\"/link/session\">Please Try Again</a>");
        }

        logger.LogInformation($"User submitted {sessionCode}");

        context.Response.Cookies.Append("UserSID", sessionId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        return Results.File(env.WebRootPath + "/SelectImgSource.html", "text/html");
    }
}