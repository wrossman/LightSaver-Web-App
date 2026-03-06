using Microsoft.AspNetCore.Antiforgery;
public static class UploadPhotosEndpoints
{
    public static void MapUploadPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/upload")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/upload", UploadPage);
        group.MapPost("/post-images", ReceiveImage);
        group.MapPost("/finish-upload", FinishUpload);
    }
    public static IResult UploadPage(IWebHostEnvironment env, IAntiforgery af, IConfiguration config, HttpContext context, ILogger<LinkSessions> logger, LinkSessions linkSessions)
    {
        var tokens = af.GetAndStoreTokens(context);

        string? linkSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out linkSessionId))
        {
            logger.LogWarning("Failed to get userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage(context, "LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        Guid sessionId;
        if (!Guid.TryParse(linkSessionId, out sessionId))
        {
            logger.LogWarning("Failed to get userid at google handle oauth response endpoint");
            return GlobalHelpers.CreateErrorPage(context, "LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        LinkSession? linkSession = linkSessions.GetSession<LinkSession>(sessionId);
        if (linkSession is null)
        {
            logger.LogWarning("Failed to get LinkSession from user id at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage(context, "Failed to retrieve your user session.", "<a href=\"/api/upload/upload\">Please Try Again</a>");
        }

        var path = Path.Combine(env.WebRootPath, "UploadImages.html");
        var html = File.ReadAllText(path);

        html = html.Replace("{{CSRF_TOKEN}}", tokens.RequestToken);
        html = html.Replace("MaxImages", config.GetValue<int>("MaxImages").ToString());
        html = html.Replace("MaxWidth", linkSession.ScreenWidth.ToString());
        html = html.Replace("MaxHeight", linkSession.ScreenHeight.ToString());

        return Results.Text(html, "text/html");
    }
    public static async Task<IResult> ReceiveImage(IConfiguration config, LinkSessions linkSessions, IFormFile image, HttpContext context, ILogger<LinkSession> logger, IAntiforgery af, GlobalStore store)
    {
        await af.ValidateRequestAsync(context);

        logger.LogInformation("Client posted to upload endpoint");

        if (!GlobalHelpers.VerifyImageUpload(image, config.GetValue<int>("MaxImages")))
        {
            logger.LogWarning("Failed to upload photos due to payload verification failure:");
            return GlobalHelpers.CreateErrorPage(context, "Failed to upload images.", "Please ensure that your images are under 10MB. Maximum file count is " + config.GetValue<int>("MaxImages").ToString());
        }

        string? linkSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out linkSessionId))
        {
            logger.LogWarning("Failed to get userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage(context, "LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        Guid sessionId;
        if (!Guid.TryParse(linkSessionId, out sessionId))
        {
            logger.LogWarning("Failed to get userid at google handle oauth response endpoint");
            return GlobalHelpers.CreateErrorPage(context, "LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        LinkSession? linkSession = linkSessions.GetSession<LinkSession>(sessionId);
        if (linkSession is null)
        {
            logger.LogWarning("Failed to get LinkSession from user id at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage(context, "Failed to retrieve your user session.", "<a href=\"/api/upload/upload\">Please Try Again</a>");
        }

        if (linkSession.Expired == true)
        {
            logger.LogWarning("User tried to upload photos with an expired session.");
            return GlobalHelpers.CreateErrorPage(context, "Your session has expired.", "<a href=\"/api/link/session\">Please Try Again</a>");
        }

        try
        {
            await store.WriteSessionImageFromUpload(sessionId, image);
        }
        catch (ArgumentOutOfRangeException)
        {
            logger.LogWarning("Too many images were attempted to be uploaded at ReceiveImage endpoint.");
            linkSessions.FailUpload(sessionId);
            linkSessions.ExpireSession(sessionId);
            return Results.BadRequest();
        }
        catch (Exception e)
        {
            logger.LogWarning("{0}: Failed to upload photos for user session.", e.Message);
            linkSessions.FailUpload(sessionId);
            linkSessions.ExpireSession(sessionId);
            return Results.InternalServerError();
        }

        return Results.Ok();
    }
    public static async Task<IResult> FinishUpload(LinkSessions linkSessions, HttpContext context, ILogger<LinkSessions> logger)
    {
        string? linkSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out linkSessionId))
        {
            logger.LogWarning("Failed to get userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage(context, "LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        Guid sessionId;
        if (!Guid.TryParse(linkSessionId, out sessionId))
        {
            logger.LogWarning("Failed to get userid at google handle oauth response endpoint");
            return GlobalHelpers.CreateErrorPage(context, "LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        LinkSession? linkSession = linkSessions.GetSession<LinkSession>(sessionId);
        if (linkSession is null)
        {
            logger.LogWarning("Failed to get LinkSession from user id at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage(context, "Failed to retrieve your user session.", "<a href=\"/api/upload/upload\">Please Try Again</a>");
        }

        linkSession.ReadyForTransfer = true;
        linkSessions.SetSession<LinkSession>(sessionId, linkSession);

        return Results.Redirect("/UploadSuccess.html");
    }
}