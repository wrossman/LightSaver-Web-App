/*
--------------------------------------------------------------------------------
 Feature & Security Improvements
--------------------------------------------------------------------------------

GENERAL TO-DO ITEMS
-------------------
- Add rate limiting to the endpoint that provides access to user photos.
- Provide access only when the user enters the correct key displayed by the Roku.
- Update all Results.* responses to their appropriate HTTP responses.
- Review and check for OWASP Top 10 vulnerabilities.
- Enable HTTPS redirection (after SSL is configured).
- Update session timeout behavior.
- If browser cookies fail, fall back to query parameters.
- Fix null-handling issues throughout the workflow.
- Restrict direct access to the image store; create public access methods
  in the ImageStore class for images and links.
- Set up proper logging for each stage of the workflow.
- Decide whether the image hash should be used as the resource link.
- Require Roku to send a hashed version of its serial number; store IDs as hashes.
- Correct the stale session service timing.
- Ensure all LogWarning() calls return something meaningful to the caller.
- Refactor static classes to enable dependency injection.
- Evaluate whether there’s a better approach to managing image resolution.

SECURITY DESIGN NOTES
---------------------
To secure access and ensure users retain proper authorization:

1. Generate a cryptographic key stored in the Roku registry.
2. Associate this key with the photo resources assigned to the user.
3. When the Roku submits the key, verify it before allowing access.
4. Consider validating the request by tying it to the device’s IP address,
   although this may not always be reliable.
5. Check whether Roku exposes a device serial number. (Confirmed: it does.)
6. Associate the Roku serial number with the images and crypto key.
7. Store the cryptographic key in the Roku’s encrypted registry.
8. Allow picture access only if the serial number matches what is stored
   for the requested image resource.

--------------------------------------------------------------------------------
*/

using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<GooglePhotosFlow>();

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

// app.UseHttpsRedirection(); //enable this once im done with getting the app service up

app.MapGooglePhotosEndpoints(); // Google Photos Feature Endpoints

// app.MapUploadEndpoint // Upload via browser endpoint will be the next feature

app.Run();
