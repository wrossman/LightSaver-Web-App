using Microsoft.Extensions.Primitives;
using System.Net;
using HtmlAgilityPack;
using System.Text.Json;
public static class GooglePhotosEndpoints
{
    public static void MapGooglePhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/google")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/google-redirect", GoogleOAuthRedirect);
        group.MapGet("/auth/google-callback", HandleOAuthResponse);
        group.MapGet("/submit-code-google", CodeSubmissionPageGoogle);
        group.MapPost("/submit", PostSessionCode);
    }
    private static IResult GoogleOAuthRedirect(IConfiguration config, ILogger<GoogleFlow> logger)
    {
        return Results.Redirect(GlobalHelpers.BuildGoogleOAuthUrl(config));
    }
    private static async Task<IResult> HandleOAuthResponse(HttpContext context, GoogleFlow google, UserSessions user, ILogger<GoogleFlow> logger)
    {
        var request = context.Request;
        var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);

        if (context.Request.Query.ContainsKey("error"))
            return GlobalHelpers.CreateErrorPage("There was a problem allowing <strong>Lightsaver</strong> to access your photos.");
        var authCode = request.Query["code"];
        if (authCode == StringValues.Empty)
            return GlobalHelpers.CreateErrorPage("There was a problem retrieving the google authorization code <strong>Lightsaver</strong> to access your photos.");
        string authCodeString = authCode.ToString();
        if (authCodeString == string.Empty)
            return GlobalHelpers.CreateErrorPage("There was a problem retrieving the google authorization code <strong>Lightsaver</strong> to access your photos.");

        string userSessionId;
        try
        {
            userSessionId = await google.GoogleAuthFlow(remoteIpAddress, authCodeString, user);
        }
        catch (Exception e)
        {
            logger.LogWarning($"Failed to complete Google Authorization Flow with error: {e.Message}");
            return GlobalHelpers.CreateErrorPage("Failed to authenticate with Google servers.");
        }

        // go to page that lets user input roku sessioncode
        // set a cookie to maintain session
        // create a fallback that uses a query to make sure it still works if browser does not allow cookies
        logger.LogInformation($"Creating cookie for userSessionID {userSessionId}");
        //thanks copilot
        context.Response.Cookies.Append("sid", userSessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"   // available to all endpoints
                         // No Expires or MaxAge → session cookie
        });

        return Results.Redirect("/google/submit-code-google");
    }
    private static IResult CodeSubmissionPageGoogle(HttpContext context, ILogger<UserSessions> logger)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/EnterSessionCodeGoogle.html"));
        string codeSubmission = doc.DocumentNode.OuterHtml;

        return Results.Content(codeSubmission, "text/html");
    }
    private static async Task<IResult> PostSessionCode(GooglePhotosFlow googlePhotos, HttpContext context, UserSessions user, ILogger<UserSessions> logger)
    {
        string? userSessionId;
        if (!context.Request.Cookies.TryGetValue("sid", out userSessionId))
            return Results.BadRequest();
        logger.LogInformation($"Session endpoint accessed sid {userSessionId} from cookie.");

        var rokuCodeForm = await context.Request.ReadFormAsync();
        if (rokuCodeForm is null)
            return Results.BadRequest();

        string? code = rokuCodeForm["code"];
        if (code is null)
            return Results.BadRequest();
        logger.LogInformation($"User submitted {code}");

        if (!await user.AssociateToRoku(code, userSessionId))
        {
            logger.LogWarning("Failed to associate roku session and user session");
            return GlobalHelpers.CreateErrorPage("The session code you entered was unable to be found.", "<a href=https://10.0.0.15:8443/google/google-redirect>Please Try Again</a>");
        }
        ;
        string pickerUri = await googlePhotos.StartGooglePhotosFlow(userSessionId, code);
        //add a check here

        return Results.Redirect($"{pickerUri}/autoclose");
    }
}