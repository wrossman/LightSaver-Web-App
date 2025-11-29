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
- how can a user report their roku lost or stolen
- verify that all images are being saved only as the max size the roku device can handle

NEW Feature ideas
-------------------
- Create an album that lets you add pictures from multiple sources. share with friends from a link.

DESIGN ITEMS
-------------------
- Create app logo
- Creating an uploading animation
- on startup, display an image that is exactly the splash screen image and have it fade out so it looks like the splash screen fades
- Create background for roku
- Web App error page creation
- Web App Code Submit Page
- Web App home page with information
- QR Code for link to code submission

WEB APP TO-DO ITEMS
-------------------
- expire lightroom update sesssions
- remove transfer file service and just set ready to transfer at methods that upload
- verify that everytime i generate a key, that it does not already exist in the db
- create separate polling endpoint for initial get
- Limit image upload size
- Add counter and upload animation
- Add a prompt that the lightroom album you linked has zero images and ask to try again
- limit the number of images to store for each upload method

ROKU TO-DO ITEMS
-------------------
- handle all response codes from httpclients
- find a way to get background image in parallel
- if the lightroom album changes and there are a fuckload of images then the httprequest will time out in intiial get
- Retry logic for failed connections
- Limit the number of keys stored on roku registry
- create an account linked progress dialog, and then show that the images are being transferred.
-------------------
DONE
-------------------
X Pass device image dimensions to web app so it can set the max image size for each device
X Create a background for each image that is just the image but super blurred. Send the background with the image if roku chooses the setting for a blurred image background.
X remove user and roku sessions after flow failure
X Evaluate whether there’s a better approach to managing image resolution.
    for google and lightroom, image resolution is chosen based off of the roku devices preferred size
    upload image gets reformatted to the max size of roku device
X Expire user credentials at flow failure
X Set up antiforgery middleware
X Add fade in animation for session code label, since it processes later
X Track if session code that was provided expired and then refresh
X Test sesssion code expiration with roku app
X Limit file size, the picture of latvia doesnt load on roku as a poster. I am assuming because it is too big?
X Create class to manage session and resource expiration
X check to see if lightroom album has changed before sending the images
X change using html agility pack to serve my html. there has to be a better way.
X Add a delete endpoint that lets you remove your files from lightsaver
X Upload images from device
X Verify polling stops if you exit out of the choose photos page
X add lightroom scraping flow
X Add failure to upload image page if cookies are disabled for upload image flow
X add site to select image source
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

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddAntiforgery();

builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("by-ip-policy", httpContext =>
    {
        string ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey: ip, factory: key => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 50,
            Window = TimeSpan.FromSeconds(20),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

//add databases
builder.Services.AddDbContext<UserSessionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddDbContext<RokuSessionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddDbContext<GlobalImageStoreDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddDbContext<LightroomUpdateSessionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

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
builder.Services.AddScoped<LightroomService>();
builder.Services.AddScoped<GlobalStoreHelpers>();
builder.Services.AddScoped<SessionHelpers>();
builder.Services.AddScoped<LightrooomUpdateSessions>();

// start all services at the same time so they dont block each other
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    services.GetRequiredService<UserSessionDbContext>().Database.Migrate();
    services.GetRequiredService<RokuSessionDbContext>().Database.Migrate();
    services.GetRequiredService<GlobalImageStoreDbContext>().Database.Migrate();
    services.GetRequiredService<LightroomUpdateSessionDbContext>().Database.Migrate();
}

app.UseRateLimiter();

app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    System.Console.WriteLine("Running in development");
}

// app.UseHttpsRedirection(); //enable this once im done with getting the app service up

app.UseAntiforgery();

app.MapGooglePhotosEndpoints(); // Google Photos Feature Endpoints

app.MapUploadPhotosEndpoints(); // Upload to web app feature enpoints

app.MapLightroomEndpoints(); // Scrape public lightroom images

app.MapRokuSessionEndpoints(); // Roku session code and image ready polling endpoints

app.MapUserSessionEndpoints(); // user session management

app.Run();
