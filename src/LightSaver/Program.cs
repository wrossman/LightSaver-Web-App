using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Azure.Identity;
using Azure.Storage.Blobs;
using Amazon.S3;
using Amazon;

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

string? saveMethod = builder.Configuration["SaveMethod"];
if (saveMethod is null)
    throw new InvalidOperationException();

// DATABASE
if (saveMethod == "aws")
{
    builder.Services.AddDbContext<GlobalImageStoreDbContext>(options =>
            options.UseSqlServer(GlobalHelpers.GetAwsConnectionString()));
}
else // FOR AZURE OR LOCAL DB
{
    builder.Services.AddDbContext<GlobalImageStoreDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
}

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
else if (saveMethod == "aws")
{
    System.Console.WriteLine("Program will be Saving resources to aws...");
    builder.Services.AddSingleton<IAmazonS3>(x =>
    {
        var config = x.GetRequiredService<IConfiguration>();

        string? awsS3Region = config["AwsS3Region"];

        if (awsS3Region is null)
        {
            System.Console.WriteLine("No AWS region was specified for S3 storage.");
            return new AmazonS3Client();
        }

        return new AmazonS3Client(RegionEndpoint.GetBySystemName(awsS3Region));
    });
    builder.Services.AddSingleton<IResourceSave, AwsSave>();
}
else
{
    System.Console.WriteLine("Program will be Saving resources to local...");
    builder.Services.AddSingleton<IResourceSave, LocalSave>();
}

var app = builder.Build();

// USE MIDDLEWARE
app.UseRateLimiter();
app.UseStaticFiles();
app.UseAntiforgery();
if (saveMethod != "aws")
{
    app.UseHttpsRedirection(); // AWS HAS ITS TRAFFIC SENT TO THE LOAD BALANCER WHICH HANDLES HTTPS
}

// MAP ENDPOINTS
app.MapGooglePhotosEndpoints();
app.MapUploadPhotosEndpoints();
app.MapLightroomEndpoints();
app.MapLinkSessionEndpoints();

app.MapGet("/", () =>
{
    return Results.Redirect("/link/session");
});

app.Run();