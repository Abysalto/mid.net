using AbySalto.Mid.Application.Common.Dtos;
using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Products.Dtos;
using AbySalto.Mid.Domain.Enums;

namespace AbySalto.Mid.Application.Products;

internal sealed class ProductService(IProductClient productClient) : IProductService
{
    // Case-insensitive lookup that yields the exact field name DummyJSON expects ("discountPercentage").
    private static readonly Dictionary<string, string> SortableFields =
        new[] { "title", "price", "rating", "stock", "discountPercentage" }
            .ToDictionary(f => f, StringComparer.OrdinalIgnoreCase);

    public Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        query = Normalize(query);

        if (query.Search is { Length: > ProductQuery.MaxSearchLength })
            throw new ValidationException(
                $"'search' must be at most {ProductQuery.MaxSearchLength} characters.", "product.invalid_query",
                new Dictionary<string, object?> { ["parameter"] = "search", ["maxLength"] = ProductQuery.MaxSearchLength });
        if (query.Category == ProductCategory.Unknown)
            throw new ValidationException("'category' must be one of the values from GET /api/products/categories.", "product.invalid_query", Param("category"));
        if (query.Page < 1)
            throw new ValidationException("'page' must be 1 or higher (pages start at 1).", "product.invalid_query", Param("page"));
        if (query.PageSize is < 1 or > ProductQuery.MaxPageSize)
            throw new ValidationException(
                $"'pageSize' must be between 1 and {ProductQuery.MaxPageSize}.", "product.invalid_query",
                new Dictionary<string, object?> { ["parameter"] = "pageSize", ["maxPageSize"] = ProductQuery.MaxPageSize });
        if (query.SortBy is not null && !SortableFields.ContainsKey(query.SortBy))
            throw new ValidationException($"'sortBy' must be one of: {string.Join(", ", SortableFields.Values)}.", "product.invalid_query", Param("sortBy"));
        if (query.Order is not null && query.Order is not ("asc" or "desc"))
            throw new ValidationException("'order' must be 'asc' or 'desc'.", "product.invalid_query", Param("order"));

        return productClient.GetProductsAsync(query, cancellationToken);
    }

    public async Task<ProductDto> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ValidationException("Product id must be a positive number.", "product.invalid_id", new Dictionary<string, object?> { ["productId"] = id });

        return await productClient.GetProductByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product {id} does not exist.", "product.not_found", new Dictionary<string, object?> { ["productId"] = id });
    }

    private static readonly IReadOnlyList<CategoryDto> Categories =
        ProductCategoryExtensions.All.Select(c => new CategoryDto(c, c.ToSlug(), c.ToDisplayName())).ToList();

    public IReadOnlyList<CategoryDto> GetCategories() => Categories;

    private static Dictionary<string, object?> Param(string name) => new() { ["parameter"] = name };

    // One canonical form per logical query: it is what DummyJSON receives and what the cache key is built from,
    // so "?sortBy=Price&order=DESC" and "?sortBy=price&order=desc" share an entry (and the first one actually sorts).
    private static ProductQuery Normalize(ProductQuery query) => query with
    {
        Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
        SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? null
            : SortableFields.GetValueOrDefault(query.SortBy.Trim(), query.SortBy),
        Order = string.IsNullOrWhiteSpace(query.Order) ? null : query.Order.Trim().ToLowerInvariant(),
    };
}
