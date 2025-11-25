using Microsoft.AspNetCore.Mvc;
public static class LightroomEndpoints
{
    public static void MapLightroomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/lightroom")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/select-album", SelectAlbum);
        group.MapPost("/post-album", PostAlbum).DisableAntiforgery();
    }
    public static IResult SelectAlbum(IWebHostEnvironment env)
    {
        return Results.File(env.WebRootPath + "/LightroomAlbumSubmit.html", "text/html");
    }
    public static async Task<IResult> PostAlbum([FromForm] string lrCode, HttpContext context, LightroomService lightroom, ILogger<LightroomService> logger)
    {
        string? userSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out userSessionId))
        {
            logger.LogWarning("Failed to get userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage("LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }
        logger.LogInformation($"Session endpoint accessed sid {userSessionId} from cookie.");

        var result = await lightroom.GetImageUrisFromShortCodeAsync(lrCode);
        var urlList = result.Item1;
        if (result.Item2 != "success" || urlList is null)
        {
            logger.LogWarning("Failed to get url list from lightroom album");
            logger.LogWarning("Failed with error: " + result.Item2);
            return GlobalHelpers.CreateErrorPage("Failed to get images from Lightroom album");
        }

        if (!await lightroom.LightroomFlow(urlList, userSessionId, lrCode))
        {
            logger.LogWarning("Failed to retreive images from Lightroom album");
            return GlobalHelpers.CreateErrorPage("Failed to retreive Lightroom album images.");
        }

        return Results.Redirect("/LightroomSuccess.html");
    }
}