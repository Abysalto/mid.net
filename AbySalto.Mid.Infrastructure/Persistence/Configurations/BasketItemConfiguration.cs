using AbySalto.Mid.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AbySalto.Mid.Infrastructure.Persistence.Configurations;

internal sealed class BasketItemConfiguration : IEntityTypeConfiguration<BasketItem>
{
    public void Configure(EntityTypeBuilder<BasketItem> builder)
    {
        builder.ToTable("BasketItems", t =>
            t.HasCheckConstraint("CK_BasketItems_Quantity", "\"Quantity\" >= 1"));
        builder.HasKey(i => i.Id);

        builder.HasIndex(i => new { i.BasketId, i.ProductId }).IsUnique();

        builder.Property(i => i.ProductTitle).HasMaxLength(256).IsRequired();
        builder.Property(i => i.Thumbnail).HasMaxLength(1024);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.AddedAt).IsRequired();

        builder.Property(i => i.Category).HasConversion<string>().HasMaxLength(64);

        builder.Ignore(i => i.LineTotal);
    }
}
