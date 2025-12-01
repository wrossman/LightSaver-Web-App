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
    private static async Task<IResult> HandleOAuthResponse(HttpContext context, UserSessions users, GooglePhotosFlow googlePhotos, GoogleFlow google, ILogger<GoogleFlow> logger)
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

        string? userSessionId;
        if (!context.Request.Cookies.TryGetValue("UserSID", out userSessionId))
        {
            logger.LogWarning("Failed to get userid at google handle oauth response endpoint");
            return GlobalHelpers.CreateErrorPage("LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        UserSession? userSession = await users.GetUserSession(userSessionId);
        if (userSession is null)
        {
            logger.LogWarning("Failed to get user session using sessionid at google handle oauth response endpoint");
            return GlobalHelpers.CreateErrorPage("There was a problem linking your google account to lightsaver.");
        }

        GoogleTokenResponse? accessTokenJson = await google.GetAccessToken(authCodeString);
        if (accessTokenJson is null)
        {
            logger.LogWarning("Failed to retrieve access token from google oauth server");
            await users.ExpireUserSession(userSession.Id);
            return GlobalHelpers.CreateErrorPage("There was a problem linking your google account to lightsaver.");
        }

        string accessToken = accessTokenJson.AccessToken;

        if (!await users.LinkAccessToken(accessToken, userSession))
        {
            logger.LogWarning("Failed to link access token with userSessionId");
            await users.ExpireUserSession(userSession.Id);
            return GlobalHelpers.CreateErrorPage("LightSaver failed to link to google.");
        }

        string pickerUri;
        try
        {
            pickerUri = await googlePhotos.StartGooglePhotosFlow(userSession);
        }
        catch
        {
            logger.LogWarning("Failed to start google photo flow for user session id " + userSession.Id);
            await users.ExpireUserSession(userSession.Id);
            return GlobalHelpers.CreateErrorPage("LightSaver is unable to connect to Google Photos.");
        }

        return Results.Redirect($"{pickerUri}/autoclose");
    }
}