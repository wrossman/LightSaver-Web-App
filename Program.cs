using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System.Net;
using HtmlAgilityPack;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
/*


ADD RATE LIMITING TO THE ENDPOINT THAT PROVIDES ACCESS TO USER PHOTOS.

PROVIDE ACCESS WHEN THEY ENTER A CORRECT KEY THAT THEIR ROKU DEVICE DISPLAYS

- Update all of the results. responses to the appropriate response

- check owasp top ten vulnerabilites

- Are the removestalesession background tasks messing with the database
  in an un thread safe way

- enable https redirect after i get ssl up

- none of this shit is thread safe

- update session timeouts

- handle if browser cookie fail revert to query

- fix null issues


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
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//add databases
builder.Services.AddDbContext<UserSessionDbContext>(options =>
    options.UseInMemoryDatabase("UserSessionDb"));
builder.Services.AddDbContext<RokuSessionDbContext>(options =>
    options.UseInMemoryDatabase("RokuSessionDb"));

//add hosted services for session management and file transfers
builder.Services.AddHostedService<RemoveStaleUserSessionsService>();
builder.Services.AddHostedService<RemoveStaleRokuSessionsService>();
builder.Services.AddHostedService<TransferFilesService>();

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

app.MapGet("/roku", async (HttpContext context, RokuSessionDbContext sessionDb) =>
{
    var request = context.Request;
    var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);

    string sessionCode = await RokuSessions.CreateRokuSession(remoteIpAddress, sessionDb);
    if (sessionCode == string.Empty)
    {
        Console.WriteLine($"Failed: TOO MANY CONNECTIONS FROM IP ADDRESS {remoteIpAddress}");
        return Results.Unauthorized();
    }

    //simulate roku device on localhost browser
    var doc = new HtmlDocument();
    doc.LoadHtml(File.ReadAllText("./wwwroot/UserSession.html"));
    var node = doc.DocumentNode.SelectSingleNode("//span[@id='sessionCode']");
    node.InnerHtml = sessionCode;
    string simulateSite = doc.DocumentNode.OuterHtml;

    //create header that roku will use to get session code
    context.Response.Headers.Append("RokuSessionCode", sessionCode);

    return Results.Content(simulateSite, "text/html");
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

app.MapGet("/auth/google-callback", async (HttpContext context, IServiceProvider serviceProvider, UserSessionDbContext userSessionDb) =>
{

    // ADD RATE LIMITING FOR ENDPOINTS
    var request = context.Request;
    var config = context.RequestServices.GetRequiredService<IConfiguration>();
    var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);


    if (context.Request.Query.ContainsKey("error"))
        return Results.Problem($"Failed with error: {context.Request.Query["error"]}");
    var authCode = request.Query["code"];
    if (authCode == StringValues.Empty)
        return Results.Problem("Unable to get Authorization Code from Google");
    string authCodeString = authCode.ToString();
    if (authCodeString == string.Empty)
        return Results.Problem("Google OAuth Response Failed to provide Authorization String");

    string userSessionId = "";
    try
    {
        userSessionId = await GoogleFlow.GoogleAuthFlow(remoteIpAddress, context, config, authCodeString, userSessionDb);
    }
    catch (Exception e)
    {
        System.Console.WriteLine(e.Message);
    }

    // go to page that lets user input roku sessioncode
    // set a cookie to maintain session
    // create a fallback that uses a query to make sure it still works if browser does not allow cookies
    Console.WriteLine($"Creating cookie for userSessionID {userSessionId}");
    //thanks copilot
    context.Response.Cookies.Append("sid", userSessionId, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/"   // available to all endpoints
                     // No Expires or MaxAge → session cookie
    });


    return Results.Redirect("/code");

});

app.MapGet("/code", async (HttpContext context) =>
{

    var doc = new HtmlDocument();
    doc.LoadHtml(File.ReadAllText("./wwwroot/EnterSessionCode.html"));
    string codeSubmission = doc.DocumentNode.OuterHtml;

    return Results.Content(codeSubmission, "text/html");

});

app.MapPost("/submit", async (IServiceProvider serviceProvider, HttpContext context, IConfiguration config, UserSessionDbContext userSessionDb, RokuSessionDbContext rokuSessionDb) =>
{
    string userSessionId;
    context.Request.Cookies.TryGetValue("sid", out userSessionId);
    Console.WriteLine($"Session endpoint accessed sid {userSessionId} from cookie.");

    var rokuCodeForm = await context.Request.ReadFormAsync();
    string code = rokuCodeForm["code"];
    System.Console.WriteLine($"User submitted {code}");

    await UserSessions.AssociateToRoku(code, userSessionId, userSessionDb, rokuSessionDb);

    GooglePhotosFlow googlePhotos = new();
    string pickerUri = await googlePhotos.StartGooglePhotosFlow(serviceProvider, config, userSessionDb, userSessionId, code);

    return Results.Redirect($"{pickerUri}/autoclose");
});

app.MapPost("/roku-reception", async (HttpContext context, RokuSessionDbContext rokuSessionDb) =>
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
        Console.WriteLine("Unknown SessionCode was tried at /roku-reception endpoint");
        return Results.NotFound("Media is not ready to be transfered.");
    }
    ;

    if (await RokuSessions.CheckReadyTransfer(sessionCode, rokuSessionDb))
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

app.MapPost("/roku-links", async (HttpContext context) =>
{
    var body = await RokuSessions.ReadRokuPost(context);
    if (body == "fail")
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    var jsonBody = JsonSerializer.Deserialize<RokuSessionPostBody>(body);
    var sessionCode = jsonBody?.RokuSessionCode;

    //thanks copilot for the query
    List<string> links = GlobalStore.GlobalImageStore
    .Where(img => img.SessionCode == sessionCode)
    .Select(img => img.Hash).ToList();

    if (links is null || links.Count == 0)
        return Results.Forbid();

    return Results.Json(new { Links = links });

});

app.MapPost("/roku-pull", async (HttpContext context) =>
{
    var body = await RokuSessions.ReadRokuPost(context);
    if (body == "fail")
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    var jsonBody = JsonSerializer.Deserialize<RokuLinksPostBody>(body);
    var link = jsonBody?.ImageLink;

    //thanks copilot for the query
    (byte[] image, string fileType) = GlobalStore.GlobalImageStore
    .Where(img => img.Hash == link)
    .Select(img => (img.ImageStream, img.FileType)).SingleOrDefault();

    if (image is null || fileType is null)
        return Results.Forbid();

    return Results.File(image, $"image/{fileType}");

});

app.Run();
