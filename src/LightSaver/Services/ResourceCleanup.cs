using Microsoft.EntityFrameworkCore;

public class ResourceCleanup : BackgroundService
{
    private readonly ILogger<ResourceCleanup> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IResourceSave _resourceSave;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromDays(1));

    public ResourceCleanup(ILogger<ResourceCleanup> logger, IServiceScopeFactory scopeFactory, IResourceSave resourceSave)
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
        try
        {
            _logger.LogInformation("Running resource cleanup.");
            using var scope = _scopeFactory.CreateScope();
            using GlobalImageStoreDbContext resourceDb = scope.ServiceProvider.GetRequiredService<GlobalImageStoreDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-30);
            var expiredSessions = await resourceDb.Resources.Where(r => r.KeyCreated < cutoff).ToListAsync(stoppingToken);

            if (expiredSessions.Count > 0)
            {
                _logger.LogInformation(
                    $"Removing {expiredSessions.Count} resources with expired keys.");

                await _resourceSave.RemoveList(expiredSessions);

                resourceDb.RemoveRange(expiredSessions);
                await resourceDb.SaveChangesAsync(stoppingToken);
            }
            else
            {
                _logger.LogInformation("No resources with keys older than cutoff were found to remove.");
                return;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to cleanup old Resources with error.");
        }
    }
    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }
}