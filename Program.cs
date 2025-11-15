using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System.Net;
using HtmlAgilityPack;
using System.Text.Json;
/*


ADD RATE LIMITING TO THE ENDPOINT THAT PROVIDES ACCESS TO USER PHOTOS.

PROVIDE ACCESS WHEN THEY ENTER A CORRECT KEY THAT THEIR ROKU DEVICE DISPLAYS

- Update all of the results. responses to the appropriate response

- check owasp top ten vulnerabilites

- Are the removestalesession background tasks messing with the database
  in an un thread safe way

- enable https redirect after i get ssl up

- update session timeouts

- handle if browser cookie fail revert to query

- fix null issues

- fix access to image store, create public access methods
  in the immage store class for access to images and links

- Set up proper logging

- Does it make sense to have the hash of the image as the resource link?

- require roku to hash the serial before sending it, store the id as a hash instead.

- ON roku what if i just set the httpagent for the currwallpaper to the one for the
  stager so i dont do a double tap for the photos, i would have to have a third agent
  to pass off to 

- Correct the stale session service time

- refactor all the static classes so i can use di


***** TO SECURE ACCESS AND MAKE SURE USERS CONTINUE TO HAVE IT,
CREATE A CRYPTOGRAPHIC KEY THAT CAN BE STORED ON THE ROKU REGISTRY
THE KEY WILL BE TIED TO THE PICTURE RESOURCE AND WILL BE OFFERED
WHEN THEY SUBMIT IT. MAYBE I COULD ALSO TIE THE IP ADDRESS TO THE KEY
TO VERIFY IF IT IS COMING FROM THE SAME SOURCE, IF NOT, DONT ALLOW IT.
THERE HAS TO BE SOME OTHER WAY FOR ME TO VERIFY THE ROKU DEVICES SIGNATURE.
MAYBE ROKU HAS A SERIAL NUMBER THAT I CAN ACCESS THAT THEY CAN PROVIDE AS WELL.
FUCK YES I JUST SEARCHED IT UP AND I CAN ACCESS THE SERIAL NUMBER. SWEET, SO
I WILL GENERATE A CRYPTO KEY AND WILL ASSOCIATE THE ROKU SERIAL NUMBER TO THE
IMAGES. ONCE I DO THAT THE ROKU CAN STORE THE CRYPTO IN ITS ENCRYPTED REGISTRY
AND THEN THEY CAN ACCESS THE PICTURES ONLY IF THE SERIAL NUMBER MATCHES THE
IMAGE RESOURCE.

*/
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

//add databases
builder.Services.AddDbContext<UserSessionDbContext>(options =>
    options.UseInMemoryDatabase("UserSessionDb"));
builder.Services.AddDbContext<RokuSessionDbContext>(options =>
    options.UseInMemoryDatabase("RokuSessionDb"));

//add hosted services for session management and file transfers
builder.Services.AddHostedService<RemoveStaleUserSessionsService>();
builder.Services.AddHostedService<RemoveStaleRokuSessionsService>();
builder.Services.AddHostedService<TransferFilesService>();

// register classes for DI
builder.Services.AddScoped<RokuSessions>();
builder.Services.AddScoped<UserSessions>();
builder.Services.AddScoped<GoogleFlow>();

// start all services at the same time so they dont block each other
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();

app.MapPost("/roku", async (HttpContext context, RokuSessions roku) =>
{
    app.Logger.LogInformation("Someone Tried to get a session code");
    var request = context.Request;
    var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);

    var body = await RokuSessions.ReadRokuPost(context);
    if (body == "fail")
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    var jsonBody = JsonSerializer.Deserialize<RokuIdPostBody>(body);
    var rokuId = jsonBody?.RokuId;

    if (string.IsNullOrEmpty(rokuId))
        return Results.BadRequest();

    string sessionCode = await roku.CreateRokuSession(remoteIpAddress, rokuId);
    if (sessionCode == string.Empty)
    {
        Console.WriteLine($"Failed: TOO MANY CONNECTIONS FROM IP ADDRESS {remoteIpAddress}");
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);

    }

    return Results.Json(new { RokuSessionCode = sessionCode });
});

app.MapGet("/google", (IConfiguration config) =>
{
    string clientId = config["OAuth:ClientId"] ?? string.Empty;
    string redirect = config["OAuth:RedirectUri"] ?? string.Empty;
    string responseType = config["OAuth:ResponseType"] ?? string.Empty;
    string scope = config["OAuth:PickerScope"] ?? string.Empty;
    string googleAuthServer = config["OAuth:GoogleAuthServer"] ?? string.Empty;
    string googleQuery = $"{googleAuthServer}?scope={scope}&response_type={responseType}&redirect_uri={redirect}&client_id={clientId}";
    return Results.Redirect(googleQuery);
});

app.MapGet("/auth/google-callback", async (HttpContext context, GoogleFlow google, UserSessions user) =>
{

    // ADD RATE LIMITING FOR ENDPOINTS
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
        return Results.BadRequest();

    string userSessionId = "";
    try
    {
        userSessionId = await google.GoogleAuthFlow(remoteIpAddress, authCodeString, user);
    }
    catch (Exception e)
    {
        System.Console.WriteLine(e.Message);
    }

    // go to page that lets user input roku sessioncode
    // set a cookie to maintain session
    // create a fallback that uses a query to make sure it still works if browser does not allow cookies
    app.Logger.LogInformation($"Creating cookie for userSessionID {userSessionId}");
    //thanks copilot
    context.Response.Cookies.Append("sid", userSessionId, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/"   // available to all endpoints
                     // No Expires or MaxAge → session cookie
    });

    return Results.Redirect("/submit-code");
});

app.MapGet("/submit-code", (HttpContext context) =>
{

    var doc = new HtmlDocument();
    doc.LoadHtml(File.ReadAllText("./wwwroot/EnterSessionCode.html"));
    string codeSubmission = doc.DocumentNode.OuterHtml;

    return Results.Content(codeSubmission, "text/html");

});

app.MapPost("/submit", async (IServiceProvider serviceProvider, HttpContext context, IConfiguration config, UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb, UserSessions user) =>
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

    GooglePhotosFlow googlePhotos = new();
    string pickerUri = await googlePhotos.StartGooglePhotosFlow(serviceProvider, config, userSessionDb, userSessionId, code);

    return Results.Redirect($"{pickerUri}/autoclose");
});

app.MapPost("/roku-reception", async (HttpContext context, RokuSessionDbContext rokuSessionDb, RokuSessions roku) =>
{
    //be carefull about what i return to the user because they cant be able to see what is a valid session code

    //thanks copilot for helping me read the post request
    var body = await RokuSessions.ReadRokuPost(context);
    if (body == "fail")
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    var jsonBody = JsonSerializer.Deserialize<RokuSessionPostBody>(body);
    var sessionCode = jsonBody?.RokuSessionCode;

    if (string.IsNullOrEmpty(sessionCode))
    {
        Console.WriteLine("An invalid SessionCode was tried at /roku-reception endpoint");
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

});

app.MapPost("/roku-get-resource-package", async (HttpContext context) =>
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

});

app.MapGet("/roku-get-resource", (HttpContext context) =>
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

});

app.Run();
