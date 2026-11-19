using AbySalto.Mid.Application.Products.Dtos;
using AbySalto.Mid.Domain.Enums;
using AbySalto.Mid.Infrastructure.ExternalApis.DummyJson;

namespace AbySalto.Mid.Application.Tests.Products;

public class InMemoryProductQueryTests
{
    private static ProductDto Product(int id, string title, decimal price, ProductCategory category) =>
        new(id, title, "", category, null, price, 0m, 4m, 10, null, []);

    private static readonly IReadOnlyList<ProductDto> SearchHits =
    [
        Product(1, "iPhone 13 Pro", 1099m, ProductCategory.Smartphones),
        Product(2, "Phone Charger", 19m, ProductCategory.MobileAccessories),
        Product(3, "Oppo A57", 249m, ProductCategory.Smartphones),
        Product(4, "Phone Case", 9m, ProductCategory.MobileAccessories),
        Product(5, "iPhone X", 899m, ProductCategory.Smartphones),
        Product(6, "Vivo X21", 499m, ProductCategory.Smartphones),
    ];

    private static ProductQuery Query(ProductCategory category, int page = 1, int pageSize = 30, string? sortBy = null, string? order = null) =>
        new() { Search = "phone", Category = category, Page = page, PageSize = pageSize, SortBy = sortBy, Order = order };

    [Fact]
    public void Keeps_only_hits_in_the_requested_category_in_search_order()
    {
        var result = InMemoryProductQuery.FilterByCategory(SearchHits, Query(ProductCategory.Smartphones));

        Assert.Equal([1, 3, 5, 6], result.Items.Select(p => p.Id));
        Assert.All(result.Items, p => Assert.Equal(ProductCategory.Smartphones, p.Category));
    }

    [Fact]
    public void Category_with_no_hits_returns_an_empty_page_with_total_zero()
    {
        var result = InMemoryProductQuery.FilterByCategory(SearchHits, Query(ProductCategory.Beauty));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void Returns_the_requested_page_and_counts_all_category_matches()
    {
        var result = InMemoryProductQuery.FilterByCategory(SearchHits, Query(ProductCategory.Smartphones, page: 2, pageSize: 2, sortBy: "price"));

        Assert.Equal([5, 1], result.Items.Select(p => p.Id));
        Assert.Equal(4, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public void A_page_past_the_end_is_empty_but_keeps_the_totals()
    {
        var result = InMemoryProductQuery.FilterByCategory(SearchHits, Query(ProductCategory.Smartphones, page: 5, pageSize: 2));

        Assert.Empty(result.Items);
        Assert.Equal(4, result.Total);
        Assert.Equal(2, result.TotalPages);
    }

    [Theory]
    [InlineData("price", "asc", new[] { 4, 2 })]
    [InlineData("price", "desc", new[] { 2, 4 })]
    [InlineData("title", "asc", new[] { 4, 2 })]
    public void Sorts_by_the_requested_field(string sortBy, string order, int[] expectedIds)
    {
        var result = InMemoryProductQuery.FilterByCategory(SearchHits, Query(ProductCategory.MobileAccessories, sortBy: sortBy, order: order));

        Assert.Equal(expectedIds, result.Items.Select(p => p.Id));
    }
}
