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
    public static async Task<IResult> PostAlbum([FromForm] string lrCode, HttpContext context, UserSessions users, LightroomService lightroom, ILogger<LightroomService> logger, IAntiforgery af)
    {
        await af.ValidateRequestAsync(context);

        string? userSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out userSessionId))
        {
            logger.LogWarning("Failed to get userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage("LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        UserSession? userSession = await users.GetUserSession(userSessionId);
        if (userSession is null)
        {
            logger.LogWarning("Failed to get userssion from userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage("Unable to retrieve your user session.", "Please Try Again");
        }

        var result = await lightroom.GetImageUrisFromShortCodeAsync(lrCode, userSession.MaxScreenSize);
        var urlList = result.Item1;
        if (result.Item2 != "success" || urlList is null)
        {
            logger.LogWarning("Failed to get url list from lightroom album");
            logger.LogWarning("Failed with error: " + result.Item2);
            await users.ExpireUserSession(userSessionId);
            return GlobalHelpers.CreateErrorPage("Failed to get images from Lightroom album", "Please ensure that your album has synced and contains photos.");
        }

        if (!await lightroom.LightroomFlow(urlList, userSession, lrCode))
        {
            logger.LogWarning("Failed to retreive images from Lightroom album");
            await users.ExpireUserSession(userSessionId);
            return GlobalHelpers.CreateErrorPage("Failed to retreive Lightroom album images.");
        }

        return Results.Redirect("/LightroomSuccess.html");
    }
}