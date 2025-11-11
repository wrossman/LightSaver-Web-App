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

    string sessionCode = await UserSessions.CreateUserSession(remoteIpAddress, sessionDb);
    if (sessionCode == string.Empty)
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
    return Results.Content(googleQuery + "\n" + sessionCode);
});

app.MapGet("/auth/google-callback", static async (HttpContext context, IServiceProvider serviceProvider) =>
{

    // ADD RATE LIMITING FOR ENDPOINTS
    var request = context.Request;
    var config = context.RequestServices.GetRequiredService<IConfiguration>();

    if (context.Request.Query.ContainsKey("error"))
        return Results.BadRequest($"Failed with error: {context.Request.Query["error"]}");
    var authCode = request.Query["code"];
    if (authCode == StringValues.Empty)
        return Results.BadRequest("Unable to get Authorization Code from Google");
    string authCodeString = authCode.ToString();
    if (authCodeString == string.Empty)
        return Results.BadRequest("Google OAuth Response Failed to provide Authorization String");

    (PickerSession, PollingConfig) pickerSession = new();
    try
    {
        GoogleFlow googleFlow = new();
        pickerSession = await googleFlow.GoogleAuthFlow(context, config, authCodeString, serviceProvider);
    }
    catch (Exception e) { return Results.BadRequest(e.Message); }

    string pickerUri = "";
    if (pickerSession.Item1 is not null && pickerSession.Item2 is not null)
        pickerUri = pickerSession.Item1.PickerUri;
    try
    {
        GooglePhotosFlow photosFlow = new();
        _ = photosFlow.StartGooglePhotosFlow(pickerSession, serviceProvider, config);
    }
    catch (Exception e) { return Results.BadRequest(e.Message); }

    return Results.Redirect($"{pickerUri}/autoclose");
});

app.Run();
