using AbySalto.Mid.Application.Baskets;
using AbySalto.Mid.Application.Baskets.Dtos;
using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Application.Products;
using AbySalto.Mid.Application.Products.Dtos;
using AbySalto.Mid.Domain.Entities;
using AbySalto.Mid.Domain.Enums;
using AbySalto.Mid.Domain.Exceptions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AbySalto.Mid.Application.Tests.Baskets;

public class BasketServiceTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid OtherUserId = Guid.CreateVersion7();

    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly IProductClient _products = Substitute.For<IProductClient>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly BasketService _sut;

    private static readonly BasketOptions Limits = new() { MaxDistinctProducts = 3, MaxQuantity = 10 };

    public BasketServiceTests()
    {
        _currentUser.UserId.Returns(UserId);
        _sut = new BasketService(_db, _products, _currentUser, Options.Create(Limits), _time);
    }

    private void GivenProduct(int id, decimal price = 10m, int stock = 50) =>
        _products.GetProductByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(new ProductDto(id, $"Product {id}", "desc", ProductCategory.Beauty, null, price, 0m, 4.5m, stock, $"thumb{id}", []));

    [Fact]
    public async Task Get_without_a_basket_returns_an_empty_one_and_creates_nothing()
    {
        var basket = await _sut.GetAsync();

        Assert.Empty(basket.Items);
        Assert.Equal(0, basket.TotalPrice);
        Assert.Empty(_db.Baskets);
    }

    [Fact]
    public async Task First_add_creates_the_basket_with_the_catalogue_price()
    {
        GivenProduct(1, price: 9.99m);

        var basket = await _sut.AddItemAsync(new AddBasketItemRequest(1, 2));

        var item = Assert.Single(basket.Items);
        Assert.Equal(9.99m, item.UnitPrice);
        Assert.Equal(ProductCategory.Beauty, item.Category);
        Assert.Equal(19.98m, basket.TotalPrice);
        Assert.Equal(UserId, Assert.Single(_db.Baskets).UserId);
    }

    [Fact]
    public async Task Adding_the_same_product_again_merges_into_one_line()
    {
        GivenProduct(1);

        await _sut.AddItemAsync(new AddBasketItemRequest(1, 2));
        var basket = await _sut.AddItemAsync(new AddBasketItemRequest(1, 3));

        Assert.Equal(5, Assert.Single(basket.Items).Quantity);
    }

    [Fact]
    public async Task Price_is_a_snapshot_and_does_not_follow_later_catalogue_changes()
    {
        GivenProduct(1, price: 10m);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 1));

        GivenProduct(1, price: 99m);
        var basket = await _sut.AddItemAsync(new AddBasketItemRequest(1, 1));

        Assert.Equal(10m, Assert.Single(basket.Items).UnitPrice);
    }

    [Fact]
    public async Task Unknown_product_is_404()
    {
        _products.GetProductByIdAsync(404, Arg.Any<CancellationToken>()).Returns((ProductDto?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.AddItemAsync(new AddBasketItemRequest(404, 1)));
    }

    [Fact]
    public async Task Cannot_add_more_than_the_stock_including_what_is_already_in_the_basket()
    {
        GivenProduct(1, stock: 3);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 2));

        var error = await Assert.ThrowsAsync<ValidationException>(() => _sut.AddItemAsync(new AddBasketItemRequest(1, 2)));

        Assert.Equal("basket.insufficient_stock", error.Code);
        Assert.Contains("you already have 2", error.Message);
        Assert.Contains("at most 1 more", error.Message);
        Assert.Equal(3, error.Details!["inStock"]);
        Assert.Equal(2, error.Details["inBasket"]);
        Assert.Equal(1, error.Details["maxAddable"]);
    }

    [Fact]
    public async Task Out_of_stock_product_gets_its_own_code()
    {
        GivenProduct(1, stock: 0);

        var error = await Assert.ThrowsAsync<ValidationException>(() => _sut.AddItemAsync(new AddBasketItemRequest(1, 1)));

        Assert.Equal("basket.out_of_stock", error.Code);
    }

    [Fact]
    public async Task Setting_a_quantity_above_the_stock_explains_the_stock()
    {
        GivenProduct(1, stock: 4);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 1));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.UpdateItemQuantityAsync(1, new UpdateBasketItemQuantityRequest(6)));

        Assert.Equal("basket.insufficient_stock", error.Code);
        Assert.Contains("can't be set to 6", error.Message);
    }

    [Fact]
    public async Task Setting_quantity_zero_points_to_delete()
    {
        GivenProduct(1);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 1));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.UpdateItemQuantityAsync(1, new UpdateBasketItemQuantityRequest(0)));

        Assert.Equal("basket.invalid_quantity", error.Code);
        Assert.Contains("DELETE /api/basket/items/1", error.Message);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 11)]
    public async Task Add_validates_product_id_and_quantity(int productId, int quantity)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _sut.AddItemAsync(new AddBasketItemRequest(productId, quantity)));
    }

    [Fact]
    public async Task Merging_lines_cannot_exceed_the_configured_max_quantity()
    {
        GivenProduct(1);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 6));

        var error = await Assert.ThrowsAsync<DomainException>(() => _sut.AddItemAsync(new AddBasketItemRequest(1, 5)));

        Assert.Equal("basket.quantity_limit_exceeded", error.Code);
        Assert.Contains("at most 4 more", error.Message);
        Assert.Equal(4, error.Details!["maxAddable"]);
    }

    [Fact]
    public async Task Basket_is_limited_to_the_configured_number_of_distinct_products()
    {
        for (var id = 1; id <= Limits.MaxDistinctProducts; id++)
        {
            GivenProduct(id);
            await _sut.AddItemAsync(new AddBasketItemRequest(id, 1));
        }

        var oneMore = Limits.MaxDistinctProducts + 1;
        GivenProduct(oneMore);
        var error = await Assert.ThrowsAsync<DomainException>(() => _sut.AddItemAsync(new AddBasketItemRequest(oneMore, 1)));

        Assert.Equal("basket.product_limit_reached", error.Code);
        Assert.Equal(Limits.MaxDistinctProducts, error.Details!["maxDistinctProducts"]);
    }

    [Fact]
    public async Task Update_sets_the_quantity()
    {
        GivenProduct(1, price: 5m);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 1));

        var basket = await _sut.UpdateItemQuantityAsync(1, new UpdateBasketItemQuantityRequest(4));

        Assert.Equal(4, Assert.Single(basket.Items).Quantity);
        Assert.Equal(20m, basket.TotalPrice);
    }

    [Fact]
    public async Task Update_or_remove_of_a_product_not_in_the_basket_is_404()
    {
        GivenProduct(1);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateItemQuantityAsync(1, new UpdateBasketItemQuantityRequest(2)));
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RemoveItemAsync(1));
    }

    [Fact]
    public async Task Remove_deletes_only_that_line()
    {
        GivenProduct(1);
        GivenProduct(2);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 1));
        await _sut.AddItemAsync(new AddBasketItemRequest(2, 1));

        var basket = await _sut.RemoveItemAsync(1);

        Assert.Equal(2, Assert.Single(basket.Items).ProductId);
    }

    [Fact]
    public async Task Clear_empties_the_basket_and_is_safe_without_one()
    {
        await _sut.ClearAsync();

        GivenProduct(1);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 1));
        await _sut.ClearAsync();

        Assert.Empty((await _sut.GetAsync()).Items);
    }

    [Fact]
    public async Task Users_only_see_their_own_basket()
    {
        GivenProduct(1);
        await _sut.AddItemAsync(new AddBasketItemRequest(1, 1));

        _currentUser.UserId.Returns(OtherUserId);

        Assert.Empty((await _sut.GetAsync()).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RemoveItemAsync(1));
    }

    [Fact]
    public async Task Without_an_authenticated_user_it_fails()
    {
        _currentUser.UserId.Returns((Guid?)null);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _sut.GetAsync());
    }
}
