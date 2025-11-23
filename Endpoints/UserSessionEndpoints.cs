using System.Net;
using HtmlAgilityPack;
public static class UserSessionEndpoints
{
    public static void MapUserSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/user")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/session", CodeSubmissionPageUpload);
        group.MapPost("/source", SelectSource);
    }
    private static IResult CodeSubmissionPageUpload(HttpContext context, ILogger<UserSessions> logger)
    {
        // append test cookie to response, read it at code submission page and redirect if cookies arent allowed
        context.Response.Cookies.Append("AllowCookie", "LightSaver", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/EnterSessionCode.html"));
        string codeSubmission = doc.DocumentNode.OuterHtml;
        return Results.Content(codeSubmission, "text/html");
    }
    private async static Task<IResult> SelectSource(UserSessions userSessions, HttpContext context, ILogger<UserSessions> logger)
    {
        // try get test cookie
        string? testCookie;
        if (!context.Request.Cookies.TryGetValue("AllowCookie", out testCookie))
            return GlobalHelpers.CreateErrorPage("Photo selection failed. LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");

        var rokuCodeForm = await context.Request.ReadFormAsync();
        if (rokuCodeForm is null)
            return Results.BadRequest();

        string? sessionCode = rokuCodeForm["code"];
        if (sessionCode is null)
            return Results.BadRequest();

        logger.LogInformation($"User submitted {sessionCode}");

        IPAddress remoteIp = context.Connection.RemoteIpAddress ?? IPAddress.None;
        string userSessionId = await userSessions.CreateUserSession(remoteIp, sessionCode);
        if (!await userSessions.AssociateToRoku(userSessionId))
        {
            logger.LogWarning("Failed to associate roku session and user session");
            return GlobalHelpers.CreateErrorPage("The session code you entered was unable to be found.", "<a href=https://10.0.0.15:8443/user/session>Please Try Again</a>");
        }

        context.Response.Cookies.Append("UserSID", userSessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/SelectImgSource.html"));
        string imgSourcePage = doc.DocumentNode.OuterHtml;

        return Results.Content(imgSourcePage, "text/html");
    }
}