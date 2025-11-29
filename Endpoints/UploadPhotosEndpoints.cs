using Microsoft.AspNetCore.Antiforgery;

public static class UploadPhotosEndpoints
{
    public static void MapUploadPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/upload")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/upload", UploadPage);
        group.MapPost("/post-images", ReceiveImages);
    }
    public static IResult UploadPage(IWebHostEnvironment env, IAntiforgery af, HttpContext context)
    {
        var tokens = af.GetAndStoreTokens(context);

        var path = Path.Combine(env.WebRootPath, "UploadImages.html");
        var html = File.ReadAllText(path);

        html = html.Replace("{{CSRF_TOKEN}}", tokens.RequestToken);

        return Results.Text(html, "text/html");
    }
    public static async Task<IResult> ReceiveImages(UploadImages upload, UserSessions users, IFormFileCollection imageCollection, HttpContext context, ILogger<UserSession> logger, IAntiforgery af)
    {
        await af.ValidateRequestAsync(context);

        List<IFormFile> images = imageCollection.ToList();

        string? userSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out userSessionId))
        {
            logger.LogWarning("Failed to get userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage("LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        UserSession? userSession = await users.GetUserSession(userSessionId);
        if (userSession is null)
        {
            logger.LogWarning("Failed to get usersession from user id at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage("Failed to retrieve your user session.", "Please Try Again");
        }

        if (!await upload.UploadImageFlow(images, userSession))
        {
            logger.LogWarning("Failed to upload photos for user session with session id " + userSession.Id);
            await users.ExpireUserSession(userSessionId);
            return GlobalHelpers.CreateErrorPage("Failed to upload images.");
        }

        return Results.Redirect("/UploadSuccess.html");
    }
}