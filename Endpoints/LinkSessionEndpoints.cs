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
    private static async Task<IResult> ProvideSessionCode(HttpContext context, LinkSessions linkSessions, ILogger<LinkSessions> logger)
    {
        // should i use middleware to manage size restriction for post bodies and then include the json body model in the signature
        string body;
        try
        {
            body = await GlobalHelpers.ReadRokuPost(context);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("An oversized payload was received at roku session code provider endpoint");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unknown error occurred: ");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var remoteIpAddress = context.Request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);
        RokuProvideSessionCodePostBody? jsonBody;
        try
        {
            jsonBody = JsonSerializer.Deserialize<RokuProvideSessionCodePostBody>(body);
            if (jsonBody is null)
            {
                logger.LogWarning("Request body could not be deserialized into RokuProvideSessionCodePostBody.");
                return Results.BadRequest("Invalid request body.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Invalid JSON received at Roku session code provider endpoint.");
            return Results.BadRequest("Invalid JSON.");
        }

        var rokuId = jsonBody.RokuId;
        var maxScreenSize = jsonBody.MaxScreenSize;
        if (string.IsNullOrEmpty(rokuId) || maxScreenSize == 0)
        {
            logger.LogWarning("Roku Id or maxScreen size was not found in roku session code provider endpoint connection.");
            return Results.BadRequest();
        }

        var sessionId = linkSessions.CreateLinkSession(remoteIpAddress, rokuId, maxScreenSize);
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
    private static async Task<IResult> RokuReception(HttpContext context, LinkSessions linkSessions, ILogger<LinkSessions> logger)
    {    //be careful about what i return to the user because they cant be able to see what is a valid session code

        logger.LogInformation("Client reached reception.");
        string body;
        try
        {
            body = await GlobalHelpers.ReadRokuPost(context);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("An oversized payload was received at roku session code provider endpoint");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unknown error occurred: ");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        RokuReceptionPostBody? jsonBody;
        try
        {
            jsonBody = JsonSerializer.Deserialize<RokuReceptionPostBody>(body);
            if (jsonBody is null)
            {
                logger.LogWarning("Request body could not be deserialized into RokuProvideSessionCodePostBody.");
                return Results.BadRequest("Invalid request body.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Invalid JSON received at Roku session code provider endpoint.");
            return Results.BadRequest("Invalid JSON.");
        }

        var sessionId = jsonBody.SessionId;
        var rokuId = jsonBody.RokuId;
        var sessionCode = jsonBody.SessionCode;

        if (sessionId == Guid.Empty || string.IsNullOrEmpty(rokuId) || string.IsNullOrEmpty(sessionCode))
        {
            logger.LogWarning("An invalid SessionCode was tried at roku reception endpoint");
            return Results.BadRequest("Media is not ready to be transferred.");
        }

        if (linkSessions.CheckExpired(sessionId, rokuId, sessionCode))
            return Results.Content("Expired");

        if (linkSessions.CheckReadyForTransfer(sessionId, rokuId, sessionCode))
            return Results.Content("Ready");
        else
            return Results.BadRequest("Media is not ready to be transferred.");
    }
    private static async Task<IResult> ProvideResourcePackage(HttpContext context, LinkSessions linkSessions, GlobalStore store, ILogger<LinkSessions> logger)
    {
        string body;
        try
        {
            body = await GlobalHelpers.ReadRokuPost(context);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("An oversized payload was received at roku session code provider endpoint");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unknown error occurred: ");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        RokuReceptionPostBody? jsonBody;
        try
        {
            jsonBody = JsonSerializer.Deserialize<RokuReceptionPostBody>(body);
            if (jsonBody is null)
            {
                logger.LogWarning("Request body could not be deserialized into RokuProvideSessionCodePostBody.");
                return Results.BadRequest("Invalid request body.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Invalid JSON received at Roku session code provider endpoint.");
            return Results.BadRequest("Invalid JSON.");
        }

        var sessionId = jsonBody.SessionId;
        var rokuId = jsonBody.RokuId;
        var sessionCode = jsonBody.SessionCode;

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
        StringValues inputMaxScreenSize;
        if (!context.Request.Headers.TryGetValue("Authorization", out inputKey)
        || !context.Request.Headers.TryGetValue("ResourceId", out inputResourceId)
        || !context.Request.Headers.TryGetValue("Device", out inputDevice)
        || !context.Request.Headers.TryGetValue("MaxScreenSize", out inputMaxScreenSize))
            return Results.Unauthorized();

        string? key = inputKey;
        Guid resourceId;
        string? rokuId = inputDevice;
        int maxScreenSize;

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(rokuId) || !Guid.TryParse(inputResourceId, out resourceId) || !Int32.TryParse(inputMaxScreenSize, out maxScreenSize))
            return Results.Unauthorized();

        logger.LogInformation($"Received Initial request from {context.Connection.RemoteIpAddress}");

        var resourceReq = new ResourceRequest(resourceId, key, rokuId, maxScreenSize);

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
        StringValues inputHeight;
        StringValues inputWidth;
        if (!context.Request.Headers.TryGetValue("Authorization", out inputKey)
        || !context.Request.Headers.TryGetValue("ResourceId", out inputResourceId)
        || !context.Request.Headers.TryGetValue("Device", out inputDevice)
        || !context.Request.Headers.TryGetValue("Height", out inputHeight)
        || !context.Request.Headers.TryGetValue("Width", out inputWidth))
            return Results.Unauthorized();

        string? key = inputKey;
        Guid resourceId;
        string? device = inputDevice;

        if (string.IsNullOrEmpty(key) ||
        string.IsNullOrEmpty(device) ||
        !Guid.TryParse(inputResourceId, out resourceId) ||
        !int.TryParse(inputHeight, out var height) ||
        !int.TryParse(inputWidth, out var width))
        {
            return Results.Unauthorized();
        }

        byte[]? image = await store.GetBackgroundData(resourceId, key, device, height, width);

        if (image is null)
        {
            return Results.Empty;
        }

        return Results.File(image, "image/webp");
    }
    public static async Task<IResult> PollUpdateLightroom(HttpContext context, ILogger<LightroomUpdateSessions> logger, LightroomUpdateSessions updateSessions)
    {
        string body;
        try
        {
            body = await GlobalHelpers.ReadRokuPost(context);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("An oversized payload was received at roku session code provider endpoint");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unknown error occurred: ");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        RokuUpdateLightroomPostBody? jsonBody;
        try
        {
            jsonBody = JsonSerializer.Deserialize<RokuUpdateLightroomPostBody>(body);
            if (jsonBody is null)
            {
                logger.LogWarning("Request body could not be deserialized into RokuProvideSessionCodePostBody.");
                return Results.BadRequest("Invalid request body.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Invalid JSON received at Roku session code provider endpoint.");
            return Results.BadRequest("Invalid JSON.");
        }

        Guid id = jsonBody.Id;
        var key = jsonBody.Key;
        var rokuId = jsonBody.RokuId;

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