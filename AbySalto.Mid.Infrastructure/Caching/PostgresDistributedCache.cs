using Microsoft.Extensions.Caching.Distributed;
using Npgsql;

namespace AbySalto.Mid.Infrastructure.Caching;

internal sealed class PostgresDistributedCache(NpgsqlDataSource dataSource, TimeProvider timeProvider) : IDistributedCache
{
    public const string TableName = "CacheEntries";

    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

    private const string GetSql =
        """SELECT "Value" FROM "CacheEntries" WHERE "Id" = $1 AND "ExpiresAt" > $2""";

    private const string SetSql =
        """
        INSERT INTO "CacheEntries" ("Id", "Value", "ExpiresAt") VALUES ($1, $2, $3)
        ON CONFLICT ("Id") DO UPDATE SET "Value" = EXCLUDED."Value", "ExpiresAt" = EXCLUDED."ExpiresAt"
        """;

    private const string RemoveSql = """DELETE FROM "CacheEntries" WHERE "Id" = $1""";

    internal const string DeleteExpiredSql = """DELETE FROM "CacheEntries" WHERE "ExpiresAt" <= $1""";

    public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        await using var command = dataSource.CreateCommand(GetSql);
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue(Now);
        return await command.ExecuteScalarAsync(token) as byte[];
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        SetAsync(key, value, options).GetAwaiter().GetResult();

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var expiresAt = ExpiresAt(options, Now);

        await using var command = dataSource.CreateCommand(SetSql);
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue(value);
        command.Parameters.AddWithValue(expiresAt);
        await command.ExecuteNonQueryAsync(token);
    }

    public void Refresh(string key) { }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await using var command = dataSource.CreateCommand(RemoveSql);
        command.Parameters.AddWithValue(key);
        await command.ExecuteNonQueryAsync(token);
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    // When both an absolute and a relative expiration are set, the earlier one wins (as in the built-in caches).
    // Sliding expiration isn't supported (Refresh is a no-op), so it is treated as a fixed lifetime;
    // HybridCache, the only caller, never sets it.
    internal static DateTime ExpiresAt(DistributedCacheEntryOptions options, DateTime now)
    {
        var relative = options.AbsoluteExpirationRelativeToNow ?? options.SlidingExpiration;
        DateTime? fromRelative = relative is { } r ? now + r : null;
        DateTime? absolute = options.AbsoluteExpiration?.UtcDateTime;

        return (absolute, fromRelative) switch
        {
            ({ } a, { } b) => a < b ? a : b,
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => now + DefaultLifetime,
        };
    }
}
