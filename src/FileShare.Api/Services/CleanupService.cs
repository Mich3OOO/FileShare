using FileShare.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FileShare.Api.Services;

public class CleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan UsedCodeRetention = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFileStorage _storage;
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(
        IServiceScopeFactory scopeFactory,
        IFileStorage storage,
        ILogger<CleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _storage = storage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cleanup pass failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var expired = await db.Files
            .Where(f => f.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var file in expired)
        {
            _storage.Delete(file.StoragePath);
            db.Files.Remove(file);
        }

        var oldUsedCodes = await db.AccessCodes
            .Where(c => c.UsedAt != null && c.UsedAt < now - UsedCodeRetention)
            .ToListAsync(cancellationToken);
        db.AccessCodes.RemoveRange(oldUsedCodes);

        await db.SaveChangesAsync(cancellationToken);

        if (expired.Count > 0 || oldUsedCodes.Count > 0)
        {
            _logger.LogInformation(
                "Cleanup: removed {Files} expired file(s) and {Codes} used code(s)",
                expired.Count, oldUsedCodes.Count);
        }
    }
}