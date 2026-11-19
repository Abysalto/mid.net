using AbySalto.Mid.Application.Common.Dtos;
using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Products;
using AbySalto.Mid.Application.Products.Dtos;
using NSubstitute;

namespace AbySalto.Mid.Application.Tests.Products;

public class ProductServiceTests
{
    private readonly IProductClient _client = Substitute.For<IProductClient>();
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _client.GetProductsAsync(Arg.Any<ProductQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<ProductDto>.Create([], 1, 30, 0));
        _sut = new ProductService(_client);
    }

    [Fact]
    public async Task Sort_field_and_order_are_passed_on_in_the_casing_DummyJSON_expects()
    {
        await _sut.GetProductsAsync(new ProductQuery { SortBy = "DISCOUNTPERCENTAGE", Order = "Desc", Search = "  phone " });

        await _client.Received(1).GetProductsAsync(
            Arg.Is<ProductQuery>(q => q.SortBy == "discountPercentage" && q.Order == "desc" && q.Search == "phone"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blank_search_is_treated_as_no_search()
    {
        await _sut.GetProductsAsync(new ProductQuery { Search = "   " });

        await _client.Received(1).GetProductsAsync(Arg.Is<ProductQuery>(q => q.Search == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_overlong_search_is_rejected_before_reaching_the_cache_or_DummyJSON()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.GetProductsAsync(new ProductQuery { Search = new string('a', ProductQuery.MaxSearchLength + 1) }));

        Assert.Equal("product.invalid_query", ex.Code);
        await _client.DidNotReceiveWithAnyArgs().GetProductsAsync(default!, default);
    }
}
