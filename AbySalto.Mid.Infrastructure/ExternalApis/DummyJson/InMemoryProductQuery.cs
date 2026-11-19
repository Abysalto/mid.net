using AbySalto.Mid.Application.Common.Dtos;
using AbySalto.Mid.Application.Products.Dtos;

namespace AbySalto.Mid.Infrastructure.ExternalApis.DummyJson;

internal static class InMemoryProductQuery
{
    public static PagedResult<ProductDto> FilterByCategory(IReadOnlyList<ProductDto> searchHits, ProductQuery query)
    {
        IEnumerable<ProductDto> matches = searchHits.Where(p => p.Category == query.Category);

        if (!string.IsNullOrWhiteSpace(query.SortBy))
            matches = Sort(matches, query.SortBy, descending: query.Order == "desc");

        var filtered = matches.ToList();
        var page = filtered.Skip(query.Skip()).Take(query.PageSize).ToList();

        return PagedResult<ProductDto>.Create(page, query.Page, query.PageSize, filtered.Count);
    }

    private static IEnumerable<ProductDto> Sort(IEnumerable<ProductDto> source, string sortBy, bool descending)
    {
        IOrderedEnumerable<ProductDto> ordered = sortBy.ToLowerInvariant() switch
        {
            "title" => OrderBy(source, p => p.Title, descending, StringComparer.OrdinalIgnoreCase),
            "price" => OrderBy(source, p => p.Price, descending),
            "rating" => OrderBy(source, p => p.Rating, descending),
            "stock" => OrderBy(source, p => p.Stock, descending),
            "discountpercentage" => OrderBy(source, p => p.DiscountPercentage, descending),
            _ => source.OrderBy(p => p.Id),
        };
        return ordered.ThenBy(p => p.Id);
    }

    private static IOrderedEnumerable<ProductDto> OrderBy<TKey>(
        IEnumerable<ProductDto> source, Func<ProductDto, TKey> key, bool descending, IComparer<TKey>? comparer = null) =>
        descending ? source.OrderByDescending(key, comparer) : source.OrderBy(key, comparer);
}
