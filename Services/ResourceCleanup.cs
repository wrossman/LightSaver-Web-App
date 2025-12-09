public class ResourceCleanup : IHostedService, IDisposable
{
    private int executionCount = 0;
    private readonly ILogger<ResourceCleanup> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer = null;

    public ResourceCleanup(ILogger<ResourceCleanup> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Resource Cleanup service is running ");

        _timer = new Timer(DoWork, null, TimeSpan.Zero,
            TimeSpan.FromDays(1));

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        var count = Interlocked.Increment(ref executionCount);

        _logger.LogInformation(
            "Cleaning up Resources. Times cleanup has ran: {Count}", count);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            using GlobalImageStoreDbContext resourceDb = scope.ServiceProvider.GetRequiredService<GlobalImageStoreDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-30);
            var expiredSessions = resourceDb.Resources.Where(r => r.KeyCreated < cutoff).ToArray();
            if (expiredSessions.Length > 0)
            {
                _logger.LogInformation(
                    "Removing {Count} resources with expired keys.", expiredSessions.Length);
                resourceDb.RemoveRange(expiredSessions);
                resourceDb.SaveChanges();
            }
            else
            {
                _logger.LogInformation("No resources with keys older than cutoff were found to remove.");
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Failed to cleanup old Resources with error: {e.Message}");
        }
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}