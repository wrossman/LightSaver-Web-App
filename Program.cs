using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Primitives;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();



// picker scope https://www.googleapis.com/auth/photospicker.mediaitems.readonly
// google oauth endpoint https://accounts.google.com/o/oauth2/v2/auth

app.MapGet("/test", (IConfiguration config) =>
{
    string clientId = config["OAuth:ClientId"] ?? string.Empty;
    string scope = config["OAuth:PickerScope"] ?? string.Empty;
    string responseType = config["OAuth:ResponseType"] ?? string.Empty;
    string redirect = config["OAuth:RedirectUri"] ?? string.Empty;
    string googleAuthServer = config["OAuth:GoogleAuthServer"] ?? string.Empty;
    string googleQuery = $"{googleAuthServer}?scope={scope}&response_type={responseType}&redirect_uri={redirect}&client_id={clientId}";
    return Results.Redirect(googleQuery);
});

app.MapGet("/auth/google-callback", async (HttpRequest request, IConfiguration config) =>
{
    var code = request.Query["code"];
    var scope = request.Query["scope"];

    GoogleAuth.AuthCode = code;
    GoogleAuth.AuthScope = scope;

    string clientId = config["OAuth:ClientId"] ?? string.Empty;
    string retrieveTokenUrl = $"https://oauth2.googleapis.com/token";

    HttpClient client = new();

    var payload = new Dictionary<string, string>
    {
        { "client_id", clientId },
        { "code", GoogleAuth.AuthCode },
        { "grant_type", "authorization_code" },
        { "redirect_uri", "https://localhost:8443/auth/google-callback/tokenGrant" }
    };

    var postContent = new FormUrlEncodedContent(payload);

    var response = await client.PostAsync(retrieveTokenUrl, postContent);
    var respContent = await response.Content.ReadAsStringAsync();

    var jsonResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(respContent);

    GoogleAuth.AccessToken = jsonResponse.AccessToken;
    GoogleAuth.ScopeGiven = jsonResponse.Scope;

    HttpClient photosClient = new();

    photosClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GoogleAuth.AccessToken);

    return $"This is my token: {GoogleAuth.AccessToken} and this is my scope: {GoogleAuth.ScopeGiven}";

    return $"This is the code: {code} and this is the scope: {scope}";
});

// app.MapGet("/auth/google-callback/token", async (IConfiguration config) =>
// {
//     string clientId = config["OAuth:ClientId"] ?? string.Empty;
//     string retrieveTokenUrl = $"https://oauth2.googleapis.com/token";

//     HttpClient client = new();

//     var payload = new Dictionary<string, string>
//     {
//         { "client_id", clientId },
//         { "code", GoogleAuth.AuthCode },
//         { "grant_type", "authorization_code" },
//         { "redirect_uri", "https://localhost:8443/auth/google-callback/tokenGrant" }
//     };

//     var postContent = new FormUrlEncodedContent(payload);

//     var response = await client.PostAsync(retrieveTokenUrl, postContent);
//     var respContent = await response.Content.ReadAsStringAsync();

//     var jsonResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(respContent);

//     GoogleAuth.AccessToken = jsonResponse.AccessToken;
//     GoogleAuth.ScopeGiven = jsonResponse.Scope;

//     HttpClient photosClient = new();

//     photosClient.DefaultRequestHeaders.Authorization =
//         new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GoogleAuth.AccessToken);

//     return $"This is my token: {GoogleAuth.AccessToken} and this is my scope: {GoogleAuth.ScopeGiven}";

// });

app.MapGet("/auth/google-callback/tokenGrant", (HttpRequest request) =>
{
    foreach (var item in request.Query)
    {
        Console.WriteLine($"{item.Key} = {item.Value}");
    }
    return Results.Ok("Query parameters printed to console.");
});

app.Run();
