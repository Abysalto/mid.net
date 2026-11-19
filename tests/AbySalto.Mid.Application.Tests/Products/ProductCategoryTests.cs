using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Products;
using AbySalto.Mid.Application.Products.Dtos;
using AbySalto.Mid.Domain.Enums;

namespace AbySalto.Mid.Application.Tests.Products;

public class ProductCategoryTests
{
    [Fact]
    public void Every_category_has_a_unique_lowercase_kebab_slug()
    {
        var slugs = ProductCategoryExtensions.All.Select(c => c.ToSlug()).ToList();

        Assert.All(slugs, s => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", s));
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    [Fact]
    public void Slugs_round_trip()
    {
        foreach (var category in ProductCategoryExtensions.All)
            Assert.Equal(category, ProductCategoryExtensions.FromSlug(category.ToSlug()));
    }

    [Theory]
    [InlineData(ProductCategory.HomeDecoration, "home-decoration", "Home Decoration")]
    [InlineData(ProductCategory.WomensJewellery, "womens-jewellery", "Womens Jewellery")]
    [InlineData(ProductCategory.Beauty, "beauty", "Beauty")]
    public void Slug_and_display_name(ProductCategory category, string slug, string name)
    {
        Assert.Equal(slug, category.ToSlug());
        Assert.Equal(name, category.ToDisplayName());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("brand-new-category")]
    public void Unmapped_slug_is_Unknown(string? slug)
    {
        Assert.Equal(ProductCategory.Unknown, ProductCategoryExtensions.FromSlug(slug));
    }

    [Fact]
    public void Categories_endpoint_lists_every_category_but_Unknown()
    {
        var sut = new ProductService(Substitute.For<IProductClient>());

        var categories = sut.GetCategories();

        Assert.Equal(ProductCategoryExtensions.All.Count, categories.Count);
        Assert.DoesNotContain(categories, c => c.Category == ProductCategory.Unknown);
    }

    [Fact]
    public async Task Filtering_by_Unknown_is_rejected()
    {
        var sut = new ProductService(Substitute.For<IProductClient>());

        await Assert.ThrowsAsync<ValidationException>(() =>
            sut.GetProductsAsync(new ProductQuery { Category = ProductCategory.Unknown }));
    }
}
