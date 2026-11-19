using System.Net;
using System.Net.Http.Json;
using AbySalto.Mid.Application.Common.Dtos;
using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Products;
using AbySalto.Mid.Application.Products.Dtos;
using AbySalto.Mid.Domain.Enums;
using AbySalto.Mid.Infrastructure.ExternalApis.DummyJson.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace AbySalto.Mid.Infrastructure.ExternalApis.DummyJson;

internal sealed record UpstreamPage(IReadOnlyList<ProductDto> Items, int Total);

// Wraps a single-product lookup so "not found" is cached too (Product = null).
internal sealed record ProductLookup(ProductDto? Product);

internal sealed class DummyJsonProductClient(
    HttpClient http,
    HybridCache cache,
    IOptions<DummyJsonOptions> options,
    ILogger<DummyJsonProductClient> logger) : IProductClient
{
    private const string Select = "select=title,description,category,brand,price,discountPercentage,rating,stock,thumbnail,images";

    // Bump when a cached shape (ProductDto, UpstreamPage, ProductLookup) changes, so a new deploy
    // never deserializes rows an older version wrote to the shared L2 table.
    private const string KeyPrefix = "dummyjson:v1:";

    private readonly HybridCacheEntryOptions? _entryOptions = CreateEntryOptions(options.Value);

    private static HybridCacheEntryOptions? CreateEntryOptions(DummyJsonOptions options)
    {
        if (options.CacheMinutes <= 0)
            return null;

        var expiration = TimeSpan.FromMinutes(options.CacheMinutes);
        var local = TimeSpan.FromSeconds(options.LocalCacheSeconds);
        return new HybridCacheEntryOptions
        {
            Expiration = expiration,
            LocalCacheExpiration = local < expiration ? local : expiration,
        };
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(query.Search) && query.Category is not null)
        {
            var hits = await GetAllSearchHitsAsync(query.Search, cancellationToken);
            return InMemoryProductQuery.FilterByCategory(hits, query);
        }

        var page = await GetPageAsync(BuildListUrl(query), cancellationToken);
        return PagedResult<ProductDto>.Create(page.Items, query.Page, query.PageSize, page.Total);
    }

    private async Task<IReadOnlyList<ProductDto>> GetAllSearchHitsAsync(string search, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync($"products/search?q={EscapeSearch(search)}&limit=0&{Select}", cancellationToken);
        return page.Items;
    }

    private Task<UpstreamPage> GetPageAsync(string url, CancellationToken cancellationToken)
    {
        return GetCachedAsync(url, async ct =>
        {
            var response = await SendAsync<DummyJsonProductsResponse>(url, ct)
                ?? throw new ProductCatalogUnavailableException("The product catalogue returned an empty response.");

            return new UpstreamPage(response.Products.Select(Map).ToList(), response.Total);
        }, cancellationToken);
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var url = $"products/{id}?{Select}";
        var lookup = await GetCachedAsync(url, async ct =>
        {
            var product = await SendAsync<DummyJsonProduct>(url, ct);
            return new ProductLookup(product is null ? null : Map(product));
        }, cancellationToken);
        return lookup.Product;
    }

    private static string BuildListUrl(ProductQuery query)
    {
        var path = !string.IsNullOrWhiteSpace(query.Search)
            ? $"products/search?q={EscapeSearch(query.Search)}&"
            : query.Category is { } category
                ? $"products/category/{category.ToSlug()}?"
                : "products?";

        var url = $"{path}limit={query.PageSize}&skip={query.Skip()}&{Select}";
        if (!string.IsNullOrWhiteSpace(query.SortBy))
            url += $"&sortBy={Uri.EscapeDataString(query.SortBy)}&order={query.Order ?? "asc"}";
        return url;
    }

    // DummyJSON's search is case-insensitive, so "Phone" and "phone " share one cache entry.
    private static string EscapeSearch(string search) => Uri.EscapeDataString(search.Trim().ToLowerInvariant());

    private async Task<T> GetCachedAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        if (_entryOptions is null)
            return await factory(ct);

        return await cache.GetOrCreateAsync<Func<CancellationToken, Task<T>>, T>(
            KeyPrefix + key,
            factory,
            static (f, token) => new ValueTask<T>(f(token)),
            _entryOptions,
            cancellationToken: ct);
    }

    private async Task<T?> SendAsync<T>(string url, CancellationToken ct) where T : class
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or ExecutionRejectedException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning(ex, "DummyJSON request failed: {Url}", url);
            throw new ProductCatalogUnavailableException("The product catalogue is currently unavailable.", ex);
        }
    }

    private ProductDto Map(DummyJsonProduct p) => new(
        p.Id,
        p.Title,
        p.Description ?? string.Empty,
        MapCategory(p),
        p.Brand,
        p.Price,
        p.DiscountPercentage,
        p.Rating,
        p.Stock,
        p.Thumbnail,
        p.Images ?? []);

    private ProductCategory MapCategory(DummyJsonProduct p)
    {
        var category = ProductCategoryExtensions.FromSlug(p.Category);
        if (category == ProductCategory.Unknown)
            logger.LogWarning("DummyJSON product {ProductId} has unmapped category '{Slug}'", p.Id, p.Category);
        return category;
    }
}
