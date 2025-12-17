using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using System.Security.Cryptography;
public static class GooglePhotosEndpoints
{
    public static void MapGooglePhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/google")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/google-redirect", GoogleOAuthRedirect);
        group.MapGet("/auth/google-callback", HandleOAuthResponse);
    }
    private static IResult GoogleOAuthRedirect(ILogger<GooglePhotosFlow> logger, HttpContext context, IConfiguration config, IMemoryCache sessionCache)
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var stateKey = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        context.Response.Cookies.Append("state", stateKey, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
        return Results.Redirect(GlobalHelpers.BuildGoogleOAuthUrl(config, stateKey));
    }
    private static async Task<IResult> HandleOAuthResponse(HttpContext context, LinkSessions linkSessions, GooglePhotosFlow googlePhotos, ILogger<GooglePhotosFlow> logger)
    {

        var returnedStateKey = context.Request.Query["state"];
        if (StringValues.IsNullOrEmpty(returnedStateKey))
            return GlobalHelpers.CreateErrorPage("There was a problem retrieving the google authorization code.");
        string returnedStateKeyString = returnedStateKey.ToString();

        string? storedStateKey;
        if (!context.Request.Cookies.TryGetValue("state", out storedStateKey))
        {
            logger.LogWarning("Failed to get state from cookie at google handle oauth response endpoint");
            return GlobalHelpers.CreateErrorPage("LightSaver requires cookies to be enabled to link your devices.", "Please enable Cookies and try again.");
        }

        logger.LogInformation($"Stored state key: {storedStateKey} returned state key: {returnedStateKeyString}");

        if (storedStateKey != returnedStateKey)
        {
            logger.LogWarning("Cookie state key and return state key from google oauth did not match.");
            return GlobalHelpers.CreateErrorPage("An error occurred when trying to link your account.", "Please ensure cookies are enabled and try again.");
        }

        if (context.Request.Query.ContainsKey("error"))
            return GlobalHelpers.CreateErrorPage("There was a problem allowing <strong>Lightsaver</strong> to access your photos.");

        var authCode = context.Request.Query["code"];
        if (StringValues.IsNullOrEmpty(authCode))
            return GlobalHelpers.CreateErrorPage("There was a problem retrieving the google authorization code.");
        string authCodeString = authCode.ToString();

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

        GoogleTokenResponse? accessTokenJson = await googlePhotos.GetAccessToken(authCodeString);
        if (accessTokenJson is null)
        {
            logger.LogWarning("Failed to retrieve access token from google oauth server");
            linkSessions.ExpireSession(sessionId);
            return GlobalHelpers.CreateErrorPage("There was a problem linking your google account to lightsaver.");
        }

        if (!linkSessions.LinkAccessToken(accessTokenJson.AccessToken, sessionId))
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