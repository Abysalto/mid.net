namespace AbySalto.Mid.Infrastructure.Caching;

internal sealed class CacheEntry
{
    public string Id { get; set; } = string.Empty;
    public byte[] Value { get; set; } = [];
    public DateTime ExpiresAt { get; set; }
}
