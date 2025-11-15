using Microsoft.Extensions.Primitives;
using System.Net;
using HtmlAgilityPack;
using System.Text.Json;
public static class GooglePhotosEndpoints
{
    public static void MapGooglePhotosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/google");

        group.MapPost("/roku", ProvideSessionCode);
        group.MapGet("/google-redirect", GoogleOAuthRedirect);
        group.MapGet("/auth/google-callback", HandleOAuthResponse);
        group.MapGet("/submit-code", CodeSubmissionPage);
        group.MapPost("/submit", PostSessionCode);
        group.MapPost("/roku-reception", RokuReception);
        group.MapPost("/roku-get-resource-package", (Delegate)ProvideResourcePackage);
        group.MapGet("/roku-get-resource", ProvideResource);
    }

    private static async Task<IResult> ProvideSessionCode(HttpContext context, RokuSessions roku, ILogger<RokuSessions> logger)
    {
        var request = context.Request;
        var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);

        var body = await RokuSessions.ReadRokuPost(context);
        if (body == "fail")
        {
            logger.LogWarning("An oversized payload was received at /google/roku");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var jsonBody = JsonSerializer.Deserialize<RokuIdPostBody>(body);
        var rokuId = jsonBody?.RokuId;

        if (string.IsNullOrEmpty(rokuId))
        {
            logger.LogWarning("Roku Id was not found in /google/roku endpoint connection.");
            return Results.BadRequest();
        }

        string sessionCode = await roku.CreateRokuSession(remoteIpAddress, rokuId);
        if (sessionCode == string.Empty)
        {
            logger.LogWarning($"Failed: TOO MANY CONNECTIONS FROM IP ADDRESS {remoteIpAddress}");
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        return Results.Json(new { RokuSessionCode = sessionCode });
    }
    private static IResult GoogleOAuthRedirect(IConfiguration config, ILogger<GoogleFlow> logger)
    {
        return Results.Redirect(GlobalHelpers.BuildGoogleOAuthUrl(config));
    }
    private static async Task<IResult> HandleOAuthResponse(HttpContext context, GoogleFlow google, UserSessions user, ILogger<GoogleFlow> logger)
    {    // ADD RATE LIMITING FOR ENDPOINTS
        var request = context.Request;
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);

        if (context.Request.Query.ContainsKey("error"))
            return Results.Content(GlobalHelpers.CreateErrorPage("There was a problem allowing <strong>Lightsaver</strong> to access your photos."), "text/html");
        var authCode = request.Query["code"];
        if (authCode == StringValues.Empty)
            return Results.Content(GlobalHelpers.CreateErrorPage("There was a problem retrieving the google authorization code <strong>Lightsaver</strong> to access your photos."), "text/html");
        string authCodeString = authCode.ToString();
        if (authCodeString == string.Empty)
            return Results.Content(GlobalHelpers.CreateErrorPage("There was a problem retrieving the google authorization code <strong>Lightsaver</strong> to access your photos."), "text/html");

        string userSessionId = "";
        try
        {
            userSessionId = await google.GoogleAuthFlow(remoteIpAddress, authCodeString, user);
        }
        catch (Exception e)
        {
            logger.LogWarning($"Failed to complete Google Authorization Flow with error: {e.Message}");
            return Results.Content(GlobalHelpers.CreateErrorPage("Failed to authenticate with Google servers."), "text/html");
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

        return Results.Redirect("/google/submit-code");
    }
    private static IResult CodeSubmissionPage(HttpContext context, ILogger<UserSessions> logger)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(File.ReadAllText("./wwwroot/EnterSessionCode.html"));
        string codeSubmission = doc.DocumentNode.OuterHtml;

        return Results.Content(codeSubmission, "text/html");
    }
    private static async Task<IResult> PostSessionCode(GooglePhotosFlow googlePhotos, HttpContext context, UserSessions user, ILogger<UserSessions> logger)
    {
        string? userSessionId;
        if (!context.Request.Cookies.TryGetValue("sid", out userSessionId))
            return Results.BadRequest();
        Console.WriteLine($"Session endpoint accessed sid {userSessionId} from cookie.");

        var rokuCodeForm = await context.Request.ReadFormAsync();
        if (rokuCodeForm is null)
            return Results.BadRequest();

        string? code = rokuCodeForm["code"];
        if (code is null)
            return Results.BadRequest();
        System.Console.WriteLine($"User submitted {code}");

        if (!await user.AssociateToRoku(code, userSessionId))
        {
            System.Console.WriteLine("Failed to associate roku session and user session");
            return Results.BadRequest();
        }
        ;
        string pickerUri = await googlePhotos.StartGooglePhotosFlow(userSessionId, code);
        //add a check here

        return Results.Redirect($"{pickerUri}/autoclose");
    }
    private static async Task<IResult> RokuReception(HttpContext context, RokuSessionDbContext rokuSessionDb, RokuSessions roku, ILogger<RokuSessions> logger)
    {    //be carefull about what i return to the user because they cant be able to see what is a valid session code

        //thanks copilot for helping me read the post request
        var body = await RokuSessions.ReadRokuPost(context);
        if (body == "fail")
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        var jsonBody = JsonSerializer.Deserialize<RokuSessionPostBody>(body);
        var sessionCode = jsonBody?.RokuSessionCode;

        if (string.IsNullOrEmpty(sessionCode))
        {
            Console.WriteLine("An invalid SessionCode was tried at /google/roku-reception endpoint");
            return Results.NotFound("Media is not ready to be transfered.");
        }
    ;

        if (await roku.CheckReadyTransfer(sessionCode))
        {
            System.Console.WriteLine("Media is ready");
            return Results.Content("Ready");
        }
        else
        {
            System.Console.WriteLine("Media is not ready to be transfered.");
            return Results.NotFound("Media is not ready to be transfered.");
        }
    }
    private static async Task<IResult> ProvideResourcePackage(HttpContext context, ILogger<RokuSessions> logger)
    {
        System.Console.WriteLine("Providing resource package");
        var body = await RokuSessions.ReadRokuPost(context);
        if (body == "fail")
        {
            System.Console.WriteLine("Failed to get Roku body");
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        var jsonBody = JsonSerializer.Deserialize<RokuSessionIdPostBody>(body);
        var sessionCode = jsonBody?.RokuSessionCode;
        var rokuId = jsonBody?.RokuId;

        System.Console.WriteLine($"User provided sessionCode: {sessionCode} and rokuId: {rokuId}");

        if (rokuId is null || sessionCode is null)
        {
            System.Console.WriteLine("rokuid or sessioncode is null");
            return Results.Forbid();
        }

        //thanks copilot for the query
        var links = GlobalStore.GetResourcePackage(sessionCode, rokuId);

        if (links is null || links.Count == 0)
        {
            System.Console.WriteLine("links is null or 0 count");
            return Results.Forbid();
        }

        return Results.Json(links);
    }
    private static IResult ProvideResource(HttpContext context, ILogger<RokuSessions> logger)
    {
        StringValues key;
        StringValues location;
        StringValues device;
        if (!context.Request.Headers.TryGetValue("Authorization", out key))
            return Results.Unauthorized();
        if (!context.Request.Headers.TryGetValue("Location", out location))
            return Results.Unauthorized();
        if (!context.Request.Headers.TryGetValue("Device", out device))
            return Results.Unauthorized();

        System.Console.WriteLine($"Received key: {key} for file {location} from device {device}");

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(device) || string.IsNullOrEmpty(location))
            return Results.Unauthorized();

        (byte[] image, string fileType) = GlobalStore.GetResourceData(location.ToString(), key.ToString(), device.ToString());

        if (image is null || fileType is null)
            return Results.Forbid();

        return Results.File(image, $"image/{fileType}");
    }
}