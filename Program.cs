using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Azure.Identity;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

// LOGGING
builder.Logging.AddAzureWebAppDiagnostics();

// MIDDLEWARE
builder.Services.AddAntiforgery();
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
builder.Services.AddHostedService<ResourceCleanup>();

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
if (builder.Configuration["SaveMethod"] == "cloud")
{
    System.Console.WriteLine("Program will be Saving resources to cloud...");
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
    builder.Services.AddSingleton<IResourceSave, CloudSave>();
}
else
{
    System.Console.WriteLine("Program will be Saving resources to local...");
    builder.Services.AddSingleton<IResourceSave, LocalSave>();
}

var app = builder.Build();

// MIGRATE RESOURCE DB
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    services.GetRequiredService<GlobalImageStoreDbContext>().Database.Migrate();
}

// USE MIDDLEWARE
app.UseRateLimiter();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseHttpsRedirection();

// MAP ENDPOINTS
app.MapGooglePhotosEndpoints();
app.MapUploadPhotosEndpoints();
app.MapLightroomEndpoints();
app.MapLinkSessionEndpoints();

app.Run();