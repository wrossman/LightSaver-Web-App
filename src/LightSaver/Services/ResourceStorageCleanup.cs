using Microsoft.EntityFrameworkCore;

public class ResourceStorageCleanup : BackgroundService
{
    private readonly ILogger<ResourceStorageCleanup> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IResourceSave _resourceSave;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromDays(1));

    public ResourceStorageCleanup(ILogger<ResourceStorageCleanup> logger, IServiceScopeFactory scopeFactory, IResourceSave resourceSave)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _resourceSave = resourceSave;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanup(stoppingToken);
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanup(stoppingToken);
        }
    }
    protected async Task RunCleanup(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Running resource storage cleanup.");
        using var scope = _scopeFactory.CreateScope();
        using GlobalImageStoreDbContext resourceDb = scope.ServiceProvider.GetRequiredService<GlobalImageStoreDbContext>();

        var fileNames = await _resourceSave.GetResources();

        if (fileNames.Count < 1)
        {
            _logger.LogInformation("No files found in storage. Skipping resource storage cleanup.");
            return;
        }

        var filesToRemove = new List<ImageShare>();

        foreach (var item in fileNames)
        {
            if (item is null || item.EndsWith("_bg"))
                continue;

            Guid itemGuid = Guid.Empty;
            Guid.TryParse(item, out itemGuid);

            if (itemGuid == Guid.Empty)
            {
                _logger.LogWarning("Failed to parse storage container item name as Guid");
                continue;
            }

            var resourceExists = await resourceDb.Resources.FirstOrDefaultAsync(r => r.Id == itemGuid, cancellationToken: stoppingToken);

            if (resourceExists is null)
            {
                filesToRemove.Add(new ImageShare()
                {
                    Id = itemGuid
                });
            }
        }

        await _resourceSave.RemoveList(filesToRemove);

        _logger.LogInformation($"Deleted {filesToRemove.Count} items from resource storage since they are no longer tracked in DB");

    }
    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }
}