using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;

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
builder.Services.AddHostedService<RemoveStaleLightroomUpdateSessionsService>();

// register classes for DI
builder.Services.AddScoped<RokuSessions>();
builder.Services.AddScoped<UserSessions>();
builder.Services.AddScoped<GoogleFlow>();
builder.Services.AddScoped<GooglePhotosFlow>();
builder.Services.AddScoped<UploadImages>();
builder.Services.AddScoped<LightroomService>();
builder.Services.AddScoped<GlobalStoreHelpers>();
builder.Services.AddScoped<SessionHelpers>();
builder.Services.AddScoped<LightroomUpdateSessions>();

// start all services at the same time so they dont block each other
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
