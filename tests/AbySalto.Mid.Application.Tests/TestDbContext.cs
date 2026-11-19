using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbySalto.Mid.Application.Tests;

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Basket> Baskets => Set<Basket>();
    public DbSet<BasketItem> BasketItems => Set<BasketItem>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    public static TestDbContext Create() =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Favorite>().HasKey(f => new { f.UserId, f.ProductId });
        modelBuilder.Entity<Basket>().Navigation(b => b.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
