using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery();

builder.Logging.AddAzureWebAppDiagnostics();

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

//add database
builder.Services.AddDbContext<GlobalImageStoreDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// add cache for sessions
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<LinkSessions>();
builder.Services.AddSingleton<LightroomUpdateSessions>();
builder.Services.AddSingleton<HmacService>();

// register classes for DI
builder.Services.AddScoped<GoogleOAuthFlow>();
builder.Services.AddScoped<GooglePhotosFlow>();
builder.Services.AddScoped<UploadImages>();
builder.Services.AddScoped<LightroomService>();
builder.Services.AddScoped<GlobalStoreHelpers>();


// start all services at the same time so they don't block each other
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
});

// configure kestrel and forms to allow big uploads from upload service
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 600L * 1000 * 1000;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 600L * 1000 * 1000;
});

var app = builder.Build();

app.UseRateLimiter();
app.UseStaticFiles();
app.UseAntiforgery();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    services.GetRequiredService<GlobalImageStoreDbContext>().Database.Migrate();
}

app.UseHttpsRedirection(); //enable this once im done with getting the app service up

app.MapGooglePhotosEndpoints(); // Google Photos Feature Endpoints

app.MapUploadPhotosEndpoints(); // Upload to web app feature endpoints

app.MapLightroomEndpoints(); // Scrape public lightroom images

app.MapLinkSessionEndpoints(); // user session management

app.Run();
