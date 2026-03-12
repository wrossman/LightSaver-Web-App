using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Azure.Identity;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

string? saveMethod = builder.Configuration["SaveMethod"];
if (saveMethod is null)
    throw new InvalidOperationException();

// LOGGING
builder.Logging.AddAzureWebAppDiagnostics();

// MIDDLEWARE
builder.Services.AddMemoryCache();
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
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // allow cross site requests with af cookie only for dev
    if (saveMethod == "local")
    { options.Cookie.SameSite = SameSiteMode.None; }
});

// DATABASE
builder.Services.AddDbContext<GlobalImageStoreDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// SINGLETONS
builder.Services.AddSingleton<LinkSessions>();
builder.Services.AddSingleton<LightroomUpdateSessions>();
builder.Services.AddSingleton<HmacHelper>();
builder.Services.AddSingleton<ImageProcessors>();

// SCOPED
builder.Services.AddScoped<GooglePhotosFlow>();
builder.Services.AddScoped<LightroomService>();
builder.Services.AddScoped<GlobalStore>();

// HOSTED SERVICES and BACKGROUND TASKS
builder.Services.AddHostedService<ResourceDbCleanup>();
builder.Services.AddHostedService<ResourceStorageCleanup>();

// START ALL SERVICES CONCURRENTLY
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
});

// ALLOW KESTREL TO RECEIVE A LARGE AMOUNT OF DATA FOR IMAGE UPLOADING
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 600L * 1000 * 1000;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 600L * 1000 * 1000;
});

// SET RESOURCE SAVE METHOD
if (saveMethod == "azure")
{
    System.Console.WriteLine("Program will be Saving resources to azure...");
    builder.Services.AddSingleton(x =>
    {
        var config = x.GetRequiredService<IConfiguration>();

        string accountName = config["AzureStorage:AccountName"] ?? "lightsaver";
        string containerName = config["AzureStorage:ContainerName"] ?? "resources";

        // Use managed identity automatically
        var credential = new DefaultAzureCredential();

        var blobUri = new Uri($"https://{accountName}.blob.core.windows.net");
        return new BlobServiceClient(blobUri, credential);
    });
    builder.Services.AddSingleton<IResourceSave, AzureSave>();
}
else if (saveMethod == "local")
{
    System.Console.WriteLine("Program will be Saving resources to local...");
    builder.Services.AddSingleton<IResourceSave, LocalSave>();
}

// LOCAL DEV FRONTEND CORS ALLOW
if (saveMethod == "local")
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevFrontEnd", policy =>
        {
            policy.WithOrigins("https://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

var app = builder.Build();

// USE MIDDLEWARE
app.UseRateLimiter();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAntiforgery();

// LOCAL FRONTEND DEV CORS ALLOW
if (saveMethod == "local")
    app.UseCors("DevFrontEnd");

// MAP ENDPOINTS
app.MapGooglePhotosEndpoints();
app.MapUploadPhotosEndpoints();
app.MapLightroomEndpoints();
app.MapLinkSessionEndpoints();
app.MapSecurityEndpoints();

app.Run();