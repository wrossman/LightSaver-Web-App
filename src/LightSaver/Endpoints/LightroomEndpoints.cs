using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
public static class LightroomEndpoints
{
    public static void MapLightroomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/lightroom")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/select-album", SelectAlbum);
        group.MapPost("/post-album", PostAlbum);
    }
    public static IResult SelectAlbum(IWebHostEnvironment env, HttpContext context, IAntiforgery af)
    {
        var tokens = af.GetAndStoreTokens(context);

        var path = Path.Combine(env.WebRootPath, "LightroomAlbumSubmit.html");
        var html = File.ReadAllText(path);

        html = html.Replace("{{CSRF_TOKEN}}", tokens.RequestToken);

        return Results.Text(html, "text/html");
    }
    public static async Task<IResult> PostAlbum([FromForm] string lrCode, IConfiguration config, HttpContext context, GlobalStore store, LinkSessions linkSessions, LightroomService lightroom, ILogger<LightroomService> logger, IAntiforgery af)
    {
        await af.ValidateRequestAsync(context);

        string? linkSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out linkSessionId))
        {
            logger.LogWarning("Failed to get userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage("LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }
        Guid sessionId;
        if (!Guid.TryParse(linkSessionId, out sessionId))
        {
            logger.LogWarning("Failed to get userid at google handle oauth response endpoint");
            return GlobalHelpers.CreateErrorPage("LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        LinkSession? linkSession = linkSessions.GetSession<LinkSession>(sessionId);
        if (linkSession is null)
        {
            logger.LogWarning("Failed to get session from userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage("Unable to retrieve your user session.", "<a href\"/link/session\">Please Try Again</a>");
        }

        if (linkSession.Expired == true)
        {
            logger.LogWarning("User tried to upload photos with an expired session.");
            return GlobalHelpers.CreateErrorPage("Your session has expired.", "<a href=\"/link/session\">Please Try Again</a>");
        }

        var result = await lightroom.GetImageUrisFromShortCodeAsync(lrCode, linkSession.ScreenWidth, linkSession.ScreenHeight);
        var urlList = result.Item1;
        if (result.Item2 != "success" && urlList is null)
        {
            logger.LogWarning("Failed to get url list from lightroom album");
            logger.LogWarning("Failed with error: " + result.Item2);
            linkSessions.ExpireSession(sessionId);
            return GlobalHelpers.CreateErrorPage("Failed to get images from Lightroom album", "Please ensure that your album has synced and contains photos.");
        }
        else if (result.Item2 == "overflow")
        {
            return GlobalHelpers.CreateLightroomOverflowPage("Your album is too large.", config.GetValue<int>("MaxImages"), "Please edit your Lightroom album so it has less than MAXFILES images and <a href=\"/lightroom/select-album\">try again</a>");
        }
        else if (result.Item1 is null || result.Item1.Count <= 0)
        {
            logger.LogWarning("Failed to retrieve images from Lightroom album. Album had no images.");
            linkSessions.ExpireSession(sessionId);
            return GlobalHelpers.CreateErrorPage("Your Lightroom album has no images to load.", "Please update your album and <a href=\"/lightroom/select-album\">try again</a>");
        }

        try
        {
            linkSessions.SetResourcePackage(sessionId, result.Item1);
            await store.WriteSessionImages(sessionId, ImageShareSource.Lightroom, lightroomAlbum: lrCode);
        }
        catch
        {
            logger.LogWarning("Failed to retrieve images from Lightroom album");
            linkSessions.ExpireSession(sessionId);
            return GlobalHelpers.CreateErrorPage("Failed to retrieve Lightroom album images.");
        }

        return Results.Redirect("/LightroomSuccess.html");
    }
}