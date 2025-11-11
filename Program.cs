using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<SessionDbContext>(options =>
    options.UseInMemoryDatabase("SessionDb"));
builder.Services.AddHostedService<RemoveStaleSessionsService>();
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

app.MapGet("/test", async (IConfiguration config, HttpContext context, SessionDbContext sessionDb) =>
{
    var request = context.Request;
    var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress ?? new IPAddress(new byte[4]);

    if (!await UserSessions.CreateUserSession(remoteIpAddress, sessionDb))
    {
        Console.WriteLine("Failed");
        return Results.Unauthorized();
    }


    string clientId = config["OAuth:ClientId"] ?? string.Empty;
    string redirect = config["OAuth:RedirectUri"] ?? string.Empty;
    string responseType = config["OAuth:ResponseType"] ?? string.Empty;
    string scope = config["OAuth:PickerScope"] ?? string.Empty;
    string googleAuthServer = config["OAuth:GoogleAuthServer"] ?? string.Empty;
    string googleQuery = $"{googleAuthServer}?scope={scope}&response_type={responseType}&redirect_uri={redirect}&client_id={clientId}";
    return Results.Content(googleQuery);
});

app.MapGet("/roku", (IConfiguration config) =>
{
    string clientId = config["OAuth:ClientId"] ?? string.Empty;
    string redirect = config["OAuth:RedirectUri"] ?? string.Empty;
    string responseType = config["OAuth:ResponseType"] ?? string.Empty;
    string scope = config["OAuth:PickerScope"] ?? string.Empty;
    string googleAuthServer = config["OAuth:GoogleAuthServer"] ?? string.Empty;
    string googleQuery = $"{googleAuthServer}?scope={scope}&response_type={responseType}&redirect_uri={redirect}&client_id={clientId}";
    return Results.Redirect(googleQuery);
});

app.MapGet("/auth/google-callback", async (HttpContext context, IServiceProvider serviceProvider) =>
{
    var request = context.Request;
    var config = context.RequestServices.GetRequiredService<IConfiguration>();

    if (context.Request.Query.ContainsKey("error"))
        return Results.BadRequest($"Failed with error: {context.Request.Query["error"]}");

    var authCode = request.Query["code"];
    if (authCode == StringValues.Empty)
        return Results.BadRequest("Unable to get Authorization Code from Google");
    string authCodeString = authCode.ToString();
    if (authCodeString is null)
        return Results.BadRequest("Google OAuth Response Failed to provide Authorization String");

    GoogleTokenResponse? accessTokenJson = await GoogleOAuth.GetAccessToken(context, config, authCodeString);
    // ADD TRACKING FOR ACCESSTOKENS
    if (accessTokenJson is null)
        return Results.BadRequest("Failed to retrieve Access Token");
    string accessToken = accessTokenJson.AccessToken;

    (PickerSession?, PollingConfig?) pickerSession = ((PickerSession?, PollingConfig?))await GooglePhotos.GetPickerSession(context, config, accessToken);
    // CREATE POLLING INSTANCE FOR PICKERSESSION
    if (pickerSession.Item1 is null || pickerSession.Item2 is null)
        return Results.BadRequest("Failed to retrieve Picker URI");
    string pickerUri = pickerSession.Item1.PickerUri;
    try
    {
        GooglePhotos myGoogleFlow = new();
        _ = myGoogleFlow.StartGooglePhotosFlow(pickerSession, accessToken, serviceProvider, config);
    }
    catch (Exception e)
    {
        System.Console.WriteLine($"Exited with error: {e.Message}");
    }


    return Results.Redirect($"{pickerUri}/autoclose");
});

app.Run();
