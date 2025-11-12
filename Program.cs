using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Html;

var builder = WebApplication.CreateBuilder(args);
/*


ADD RATE LIMITING TO THE ENDPOINT THAT PROVIDES ACCESS TO USER PHOTOS.

PROVIDE ACCESS WHEN THEY ENTER A CORRECT KEY THAT THEIR ROKU DEVICE DISPLAYS


*/
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<UserSessionDbContext>(options =>
    options.UseInMemoryDatabase("UserSessionDb"));
builder.Services.AddDbContext<RokuSessionDbContext>(options =>
    options.UseInMemoryDatabase("RokuSessionDb"));
builder.Services.AddHostedService<RemoveStaleUserSessionsService>();
builder.Services.AddHostedService<RemoveStaleRokuSessionsService>();
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
    System.Console.WriteLine(googleQuery);
    return Results.Redirect(googleQuery);
});

app.MapGet("/auth/google-callback", async (HttpContext context, IServiceProvider serviceProvider, UserSessionDbContext userSessionDb) =>
{

    // ADD RATE LIMITING FOR ENDPOINTS
    var request = context.Request;
    var config = context.RequestServices.GetRequiredService<IConfiguration>();
    var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);


    if (context.Request.Query.ContainsKey("error"))
        return Results.BadRequest($"Failed with error: {context.Request.Query["error"]}");
    var authCode = request.Query["code"];
    if (authCode == StringValues.Empty)
        return Results.BadRequest("Unable to get Authorization Code from Google");
    string authCodeString = authCode.ToString();
    if (authCodeString == string.Empty)
        return Results.BadRequest("Google OAuth Response Failed to provide Authorization String");

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

app.MapPost("/submit", async (IServiceProvider serviceProvider, HttpContext context, IConfiguration config, UserSessionDbContext userSessionDb) =>
{
    string userSessionId;
    context.Request.Cookies.TryGetValue("sid", out userSessionId);
    Console.WriteLine($"Session endpoint accessed sid {userSessionId} from cookie.");

    var rokuCodeForm = await context.Request.ReadFormAsync();
    string code = rokuCodeForm["code"];
    System.Console.WriteLine($"User submitted {code}");

    GooglePhotosFlow googlePhotos = new();
    string pickerUri = await googlePhotos.StartGooglePhotosFlow(serviceProvider, context, config, userSessionDb, userSessionId);

    return Results.Redirect($"{pickerUri}/autoclose");
});

app.Run();
