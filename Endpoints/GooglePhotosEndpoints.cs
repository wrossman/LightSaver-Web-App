using Microsoft.Extensions.Primitives;
public static class GooglePhotosEndpoints
{
    public static void MapGooglePhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/google")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/google-redirect", GoogleOAuthRedirect);
        group.MapGet("/auth/google-callback", HandleOAuthResponse);
    }
    private static IResult GoogleOAuthRedirect(IConfiguration config)
    {
        return Results.Redirect(GlobalHelpers.BuildGoogleOAuthUrl(config));
    }
    private static async Task<IResult> HandleOAuthResponse(HttpContext context, LinkSessions linkSessions, GooglePhotosFlow googlePhotos, GoogleOAuthFlow google, ILogger<GoogleOAuthFlow> logger)
    {
        var request = context.Request;

        if (context.Request.Query.ContainsKey("error"))
            return GlobalHelpers.CreateErrorPage("There was a problem allowing <strong>Lightsaver</strong> to access your photos.");

        var authCode = request.Query["code"];
        if (authCode == StringValues.Empty)
            return GlobalHelpers.CreateErrorPage("There was a problem retrieving the google authorization code.");

        string authCodeString = authCode.ToString();
        if (authCodeString == string.Empty)
            return GlobalHelpers.CreateErrorPage("There was a problem retrieving the google authorization code.");

        string? linkSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out linkSessionId))
        {
            logger.LogWarning("Failed to get userid at google handle oauth response endpoint");
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
            logger.LogWarning("Failed to get user session using session id at google handle oauth response endpoint");
            return GlobalHelpers.CreateErrorPage("There was a problem linking your google account to lightsaver.");
        }

        if (linkSession.Expired == true)
        {
            logger.LogWarning("User tried to upload photos with an expired session.");
            return GlobalHelpers.CreateErrorPage("Your session has expired.", "<a href=\"/link/session\">Please Try Again</a>");
        }

        GoogleTokenResponse? accessTokenJson = await google.GetAccessToken(authCodeString);
        if (accessTokenJson is null)
        {
            logger.LogWarning("Failed to retrieve access token from google oauth server");
            linkSessions.ExpireSession(sessionId);
            return GlobalHelpers.CreateErrorPage("There was a problem linking your google account to lightsaver.");
        }

        if (!await linkSessions.LinkAccessToken(accessTokenJson.AccessToken, sessionId))
        {
            logger.LogWarning("Failed to link access token with LinkSessionId");
            linkSessions.ExpireSession(sessionId);
            return GlobalHelpers.CreateErrorPage("LightSaver failed to link to google.", "Please Try Again");
        }

        string pickerUri;
        try
        {
            pickerUri = await googlePhotos.StartGooglePhotosFlow(sessionId);
        }
        catch
        {
            logger.LogWarning("Failed to start google photo flow for user session");
            linkSessions.ExpireSession(sessionId);
            return GlobalHelpers.CreateErrorPage("LightSaver is unable to connect to Google Photos.");
        }

        return Results.Redirect($"{pickerUri}/autoclose");
    }
}