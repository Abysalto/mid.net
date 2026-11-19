using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Application.Favorites;
using AbySalto.Mid.Application.Products;
using AbySalto.Mid.Application.Products.Dtos;
using AbySalto.Mid.Domain.Enums;
using Microsoft.Extensions.Time.Testing;

namespace AbySalto.Mid.Application.Tests.Favorites;

public class FavoriteServiceTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly IProductClient _products = Substitute.For<IProductClient>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly FavoriteService _sut;
    private static readonly Guid Me = Guid.CreateVersion7();
    private static readonly Guid SomeoneElse = Guid.CreateVersion7();

    public FavoriteServiceTests()
    {
        _currentUser.UserId.Returns(Me);
        _sut = new FavoriteService(_db, _products, _currentUser, _time);
    }

    private void GivenProduct(int id) =>
        _products.GetProductByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(new ProductDto(id, $"Product {id}", "desc", ProductCategory.Groceries, null, 10m, 0m, 4.5m, 5, $"thumb{id}", []));

    [Fact]
    public async Task Add_stores_the_product_title_and_thumbnail()
    {
        GivenProduct(16);

        var favorite = await _sut.AddAsync(16);

        Assert.Equal("Product 16", favorite.Title);
        Assert.Equal(ProductCategory.Groceries, favorite.Category);
        Assert.Equal("thumb16", Assert.Single(_db.Favorites).Thumbnail);
    }

    [Fact]
    public async Task Adding_twice_is_idempotent_and_does_not_call_the_catalogue_again()
    {
        GivenProduct(16);

        await _sut.AddAsync(16);
        await _sut.AddAsync(16);

        Assert.Single(_db.Favorites);
        await _products.Received(1).GetProductByIdAsync(16, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_product_is_404()
    {
        _products.GetProductByIdAsync(404, Arg.Any<CancellationToken>()).Returns((ProductDto?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AddAsync(404));
    }

    [Fact]
    public async Task Get_lists_newest_first_and_only_for_the_current_user()
    {
        GivenProduct(1);
        GivenProduct(2);
        await _sut.AddAsync(1);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _sut.AddAsync(2);

        _currentUser.UserId.Returns(SomeoneElse);
        await _sut.AddAsync(1);
        _currentUser.UserId.Returns(Me);

        var favorites = await _sut.GetAsync();

        Assert.Equal([2, 1], favorites.Select(f => f.ProductId));
    }

    [Fact]
    public async Task Remove_deletes_it_and_a_second_remove_is_404()
    {
        GivenProduct(1);
        await _sut.AddAsync(1);

        await _sut.RemoveAsync(1);

        Assert.Empty(_db.Favorites);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RemoveAsync(1));
    }

    [Fact]
    public async Task Invalid_product_id_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _sut.AddAsync(0));
    }
}
