using System.Net;
using System.Text.Json;
using HtmlAgilityPack;
public static class UploadPhotosEndpoints
{
    public static void MapUploadPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/upload")
            .RequireRateLimiting("by-ip-policy");

        group.MapPost("/upload-images", ReceiveImages);
    }
    public static async Task<IResult> ReceiveImages(UploadImages upload, List<IFormFile> images, HttpContext context, GlobalImageStoreDbContext resourceDb, ILogger<UserSession> logger)
    {
        string? userSessionId;
        if (!context.Request.Cookies.TryGetValue("sid", out userSessionId))
            return Results.BadRequest();
        logger.LogInformation($"Session endpoint accessed sid {userSessionId} from cookie.");

        if (!await upload.UploadImageFlow(images, userSessionId))
        {
            logger.LogWarning("Failed to upload photos for user session with session id " + userSessionId);
        }

        return Results.Content("Upload Complete");
    }
}