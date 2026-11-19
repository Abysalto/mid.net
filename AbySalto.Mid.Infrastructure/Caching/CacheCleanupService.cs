using System.Data.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AbySalto.Mid.Infrastructure.Caching;

internal sealed class CacheCleanupService(
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    ILogger<CacheCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var command = dataSource.CreateCommand(PostgresDistributedCache.DeleteExpiredSql);
                command.Parameters.AddWithValue(timeProvider.GetUtcNow().UtcDateTime);
                var removed = await command.ExecuteNonQueryAsync(stoppingToken);
                if (removed > 0)
                    logger.LogInformation("Removed {Count} expired cache entries", removed);
            }
            catch (DbException ex)
            {
                logger.LogWarning("Cache cleanup failed ({ExceptionType} SQLSTATE {SqlState})", ex.GetType().Name, ex.SqlState);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An unhandled exception in a BackgroundService stops the whole host (.NET 8+ default),
                // and a missed cleanup only costs disk space, so log and try again next tick.
                logger.LogWarning("Cache cleanup failed ({ExceptionType})", ex.GetType().Name);
            }
        }
    }
}
