using AbySalto.Mid.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AbySalto.Mid.Infrastructure.Persistence.Configurations;

internal sealed class CacheEntryConfiguration : IEntityTypeConfiguration<CacheEntry>
{
    public void Configure(EntityTypeBuilder<CacheEntry> builder)
    {
        builder.ToTable(PostgresDistributedCache.TableName);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(1024);
        builder.Property(e => e.Value).IsRequired();
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.HasIndex(e => e.ExpiresAt);
    }
}
