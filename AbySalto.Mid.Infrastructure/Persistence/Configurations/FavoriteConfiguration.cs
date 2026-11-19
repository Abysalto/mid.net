using AbySalto.Mid.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AbySalto.Mid.Infrastructure.Persistence.Configurations;

internal sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("Favorites");

        builder.HasKey(f => new { f.UserId, f.ProductId });

        builder.Property(f => f.ProductTitle).HasMaxLength(256).IsRequired();
        builder.Property(f => f.Thumbnail).HasMaxLength(1024);
        builder.Property(f => f.CreatedAt).IsRequired();

        builder.Property(f => f.Category).HasConversion<string>().HasMaxLength(64);
    }
}
