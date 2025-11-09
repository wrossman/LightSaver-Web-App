using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
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
    string redirect = config["OAuth:RedirectUri"] ?? string.Empty;
    string responseType = config["OAuth:ResponseType"] ?? string.Empty;
    string scope = config["OAuth:PickerScope"] ?? string.Empty;
    string googleAuthServer = config["OAuth:GoogleAuthServer"] ?? string.Empty;
    string googleQuery = $"{googleAuthServer}?scope={scope}&response_type={responseType}&redirect_uri={redirect}&client_id={clientId}";
    return Results.Redirect(googleQuery);
});

app.MapGet("/auth/google-callback", async (HttpContext context) =>
{
    var request = context.Request;
    var config = context.RequestServices.GetRequiredService<IConfiguration>();

    if (request.Query.ContainsKey("error"))
    {
        var error = request.Query["error"];
        return Results.BadRequest($"Failed with error: {error}");
    }
    ;

    var code = request.Query["code"];
    var scope = request.Query["scope"];

    string clientId = config["OAuth:ClientId"] ?? string.Empty;
    string clientSecret = config["OAuth:ClientSecret"] ?? string.Empty;
    string retrieveTokenUrl = "https://oauth2.googleapis.com/token";

    using HttpClient client = new();

    var payload = new Dictionary<string, string>
    {
        { "client_id", clientId },
        { "client_secret", clientSecret},
        { "code", code },
        { "grant_type", "authorization_code" },
        { "redirect_uri", "https://localhost:8443/auth/google-callback" }
    };

    var postContent = new FormUrlEncodedContent(payload);

    var response = await client.PostAsync(retrieveTokenUrl, postContent);
    var respContent = await response.Content.ReadAsStringAsync();

    var jsonResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(respContent);
    if (jsonResponse is null || string.IsNullOrEmpty(jsonResponse.AccessToken))
    {
        return Results.Problem("Failed to retrieve access token.");
    }

    string accessToken = jsonResponse.AccessToken;
    string scopeGiven = jsonResponse.Scope;

    using HttpClient photosClient = new();

    var pickerRequest = new HttpRequestMessage(HttpMethod.Post, "https://photospicker.googleapis.com/v1/sessions");
    pickerRequest.Headers.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    pickerRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");

    var pickerResponse = await photosClient.SendAsync(pickerRequest);
    var pickerContent = await pickerResponse.Content.ReadAsStringAsync();

    var pickerJson = JsonSerializer.Deserialize<PickerSession>(pickerContent);
    if (pickerJson is null || string.IsNullOrEmpty(pickerJson.PickerUri))
    {
        return Results.Problem("Failed to create photo picker session.");
    }

    var photoRedirect = pickerJson.PickerUri;

    return Results.Redirect(photoRedirect);
});

app.Run();
