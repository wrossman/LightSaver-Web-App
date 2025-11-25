public static class UploadPhotosEndpoints
{
    public static void MapUploadPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/upload")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/upload", UploadPage);
        group.MapPost("/post-images", ReceiveImages).DisableAntiforgery();
    }
    public static IResult UploadPage(IWebHostEnvironment env)
    {
        return Results.File(env.WebRootPath + "/UploadImages.html", "text/html");
    }
    public static async Task<IResult> ReceiveImages(UploadImages upload, IFormFileCollection imageCollection, HttpContext context, ILogger<UserSession> logger)
    {
        List<IFormFile> images = imageCollection.ToList();
        logger.LogInformation("Client reached receive image endpoint");
        string? userSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out userSessionId))
        {
            logger.LogWarning("Failed to get userid at upload receive images endpoint");
            return GlobalHelpers.CreateErrorPage("LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }
        logger.LogInformation($"Session endpoint accessed sid {userSessionId} from cookie.");

        if (!await upload.UploadImageFlow(images, userSessionId))
        {
            logger.LogWarning("Failed to upload photos for user session with session id " + userSessionId);
            return GlobalHelpers.CreateErrorPage("Failed to upload images.");
        }

        return Results.Redirect("/UploadSuccess.html");
    }
}