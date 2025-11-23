using System.Net;
using System.Text.Json;
using HtmlAgilityPack;
public static class UploadPhotosEndpoints
{
    public static void MapUploadPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/upload")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/submit-code-upload", CodeSubmissionPageUpload);
        group.MapPost("/submit", PostSessionCode);
        group.MapPost("/upload-images", ReceiveImages);
    }

    private static IResult CodeSubmissionPageUpload(HttpContext context, ILogger<UserSessions> logger)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/EnterSessionCodeUpload.html"));
        string codeSubmission = doc.DocumentNode.OuterHtml;

        return Results.Content(codeSubmission, "text/html");
    }
    private static async Task<IResult> PostSessionCode(HttpContext context, UserSessions user, ILogger<UserSessions> logger)
    {
        IPAddress remoteIp = context.Connection.RemoteIpAddress ?? IPAddress.None;

        var rokuCodeForm = await context.Request.ReadFormAsync();
        if (rokuCodeForm is null)
            return Results.BadRequest();

        string? code = rokuCodeForm["code"];
        if (code is null)
            return Results.BadRequest();
        logger.LogInformation($"User submitted {code}");

        string userSessionId = await user.CreateUploadUserSession(remoteIp, code);

        if (!await user.AssociateToRoku(code, userSessionId))
        {
            logger.LogWarning("Failed to associate roku session and user session");
            return GlobalHelpers.CreateErrorPage("The session code you entered was unable to be found.", "<a href=https://10.0.0.15:8443/google/google-redirect>Please Try Again</a>");
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/UploadImages.html"));
        string uploadImagePage = doc.DocumentNode.OuterHtml;

        // add cookie to track user session on file upload page
        context.Response.Cookies.Append("sid", userSessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"   // available to all endpoints
                         // No Expires or MaxAge → session cookie
        });

        return Results.Content(uploadImagePage, "text/html");
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