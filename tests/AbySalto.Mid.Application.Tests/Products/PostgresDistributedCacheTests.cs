using AbySalto.Mid.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace AbySalto.Mid.Application.Tests.Products;

public class PostgresDistributedCacheTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_earlier_of_absolute_and_relative_expiration_wins()
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = Now.AddHours(1),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        };

        Assert.Equal(Now.AddMinutes(5), PostgresDistributedCache.ExpiresAt(options, Now));
    }

    [Fact]
    public void An_absolute_expiration_alone_is_used_as_is()
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpiration = Now.AddMinutes(3) };

        Assert.Equal(Now.AddMinutes(3), PostgresDistributedCache.ExpiresAt(options, Now));
    }

    [Fact]
    public void No_expiration_falls_back_to_ten_minutes()
    {
        Assert.Equal(Now.AddMinutes(10), PostgresDistributedCache.ExpiresAt(new DistributedCacheEntryOptions(), Now));
    }
}
