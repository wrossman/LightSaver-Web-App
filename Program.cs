/*
--------------------------------------------------------------------------------
 Feature & Security Improvements
--------------------------------------------------------------------------------

FINAL ITEMS
-------------------
- Enable HTTPS redirection (after SSL is configured).
- Review and check for OWASP Top 10 vulnerabilities.
- Update all Results.* responses to their appropriate HTTP responses.
- Remove all sensitive data from logging
- Create Privacy Policy
- Create Terms and Conditions
- Check for uncaught exceptions

NEW FEATURES
-------------------
- Upload images from device
- Create an album that lets you add pictures from multiple sources. share with friends from a link.
- Create a background for each image that is just the image but super blurred. Send the background with the image if roku chooses the setting for a blurred image background.

DESIGN ITEMS
-------------------
- Create app logo
- on startup, display an image that is exactly the splash screen image and have it fade out so it looks like the splash screen fades
- Create background for roku
- Web App error page creation
- Web App Code Submit Page
- Web App home page with information

WEB APP TO-DO ITEMS
-------------------
- Evaluate whether there’s a better approach to managing image resolution.
- remove user and roku sessions after flow failure
- QR Code for LightSaver, could i make it so it verifys if the user enables cookies,
  generate the qr code dynamically and include the session code in it, user scans
  code, the enpoint stores the code in cookies and then forwards them through the
  google oauth process without having to submit their session code
- Add failure to upload image page if cookies are disabled for upload image flow

ROKU TO-DO ITEMS
-------------------
- Fix issues with imgLinks not being initialized correctly on startup,
    if there are no links i should not be able to start the wallpaper and i should be directed elsewhere
-------------------
DONE
-------------------
X if a roku tries to get google photos again then remove old photos right before comitting new ones
X Figure out why google still shows the wrong project name
X input validation for pic display time
X Require Roku to send a hashed version of its serial number; store IDs as hashes so im not storing peoples serials
X Check if ligthroom album doesnt have any pictures before trying to display
X Fix pic display time from geeking out the slideshow if you set it too low
X Create a load config task instaed of keeping it all in main scene init
X Add image display time to registry
X Fix wallpaper not showing if only one image
X If the keys roku provides to lightsaver web app are old, prompt it to redo the flow
X Ensure all polling tasks stop when leaving the screen in roku
X fix roku pulling weird images after i select new google photos from the roku app
X remove session code from imgs after linking and providing resource package
X remove session code from session code hash set on session expiration
X remove duplicate session if the same roku device tries to connect to /roku
X Restrict direct access to the image store; create public access methods
    in the ImageStore class for images and links.
X Fix null-handling issues throughout the workflow.
X Decide whether the image hash should be used as the resource link. FOR NOW YES
X Correct the stale session service timing.
X Ensure all LogWarning() calls return something meaningful to the caller.
X Provide access only when the user enters the correct key displayed by the Roku.
X Update session timeout behavior.
X Refactor static classes to enable dependency injection.
X Set up proper logging for each stage of the workflow.
X Add rate limiting to the endpoint that provides access to user photos.
X If browser cookies fail, fall back to query parameters. OAuth requires cookies so this is not a thing.

SECURITY DESIGN NOTES
-------------------
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

-------------------
*/

using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("by-ip-policy", httpContext =>
    {
        string ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey: ip, factory: key => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(30),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

//add databases
builder.Services.AddDbContext<UserSessionDbContext>(options =>
    options.UseInMemoryDatabase("UserSessionDb"));
builder.Services.AddDbContext<RokuSessionDbContext>(options =>
    options.UseInMemoryDatabase("RokuSessionDb"));
builder.Services.AddDbContext<GlobalImageStoreDbContext>(options =>
    options.UseInMemoryDatabase("GlobalImageStore"));

//add hosted services for session management and file transfers
builder.Services.AddHostedService<RemoveStaleUserSessionsService>();
builder.Services.AddHostedService<RemoveStaleRokuSessionsService>();
builder.Services.AddHostedService<TransferFilesService>();

// register classes for DI
builder.Services.AddScoped<RokuSessions>();
builder.Services.AddScoped<UserSessions>();
builder.Services.AddScoped<GoogleFlow>();
builder.Services.AddScoped<GooglePhotosFlow>();
builder.Services.AddScoped<UploadImages>();

// start all services at the same time so they dont block each other
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
});

var app = builder.Build();

app.UseRateLimiter();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection(); //enable this once im done with getting the app service up

app.MapGooglePhotosEndpoints(); // Google Photos Feature Endpoints

app.MapUploadPhotosEndpoints(); // Upload to web app feature enpoints

app.MapRokuSessionEndpoints(); // Roku session code and image ready polling endpoints

app.Run();
