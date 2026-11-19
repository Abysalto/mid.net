using System.Collections.Concurrent;
using System.Net;
using System.Text;
using AbySalto.Mid.Application.Products.Dtos;
using AbySalto.Mid.Infrastructure.ExternalApis.DummyJson;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AbySalto.Mid.Application.Tests.Products;

public class DummyJsonCachingTests
{
    private const string ProductJson =
        """{"id":1,"title":"Essence Mascara","description":"d","category":"beauty","brand":"Essence","price":9.99,"discountPercentage":1,"rating":4.5,"stock":5,"thumbnail":"t","images":["i"]}""";

    private const string ListJson = $$"""{"products":[{{ProductJson}}],"total":194,"skip":0,"limit":1}""";

    private readonly CountingHandler _upstream = new();
    private readonly SharedTableStandIn _sharedL2 = new();

    private DummyJsonProductClient NewInstance(int cacheMinutes = 10)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache>(_sharedL2);
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();

        return new DummyJsonProductClient(
            new HttpClient(_upstream) { BaseAddress = new Uri("https://dummyjson.test/") },
            cache,
            Options.Create(new DummyJsonOptions { CacheMinutes = cacheMinutes }),
            NullLogger<DummyJsonProductClient>.Instance);
    }

    [Fact]
    public async Task A_cached_product_is_fetched_from_DummyJSON_only_once()
    {
        var client = NewInstance();

        var first = await client.GetProductByIdAsync(1);
        var second = await client.GetProductByIdAsync(1);

        Assert.Equal("Essence Mascara", second!.Title);
        Assert.Equal(first!.Price, second.Price);
        Assert.Equal(1, _upstream.Calls);
    }

    [Fact]
    public async Task Another_instance_reads_the_shared_database_cache_instead_of_DummyJSON()
    {
        var instanceA = NewInstance();
        var instanceB = NewInstance();

        await instanceA.GetProductsAsync(new ProductQuery { PageSize = 1 });
        var fromB = await instanceB.GetProductsAsync(new ProductQuery { PageSize = 1 });

        Assert.Equal(194, fromB.Total);
        Assert.Single(fromB.Items);
        Assert.Equal(1, _upstream.Calls);
        Assert.Equal(1, _sharedL2.Count);
    }

    [Fact]
    public async Task Not_found_is_cached_so_unknown_ids_do_not_hammer_DummyJSON()
    {
        var client = NewInstance();

        Assert.Null(await client.GetProductByIdAsync(404));
        Assert.Null(await client.GetProductByIdAsync(404));

        Assert.Equal(1, _upstream.Calls);
    }

    [Fact]
    public async Task Search_terms_differing_only_in_case_and_spaces_share_one_entry()
    {
        var client = NewInstance();

        await client.GetProductsAsync(new ProductQuery { Search = "Mascara" });
        await client.GetProductsAsync(new ProductQuery { Search = " mascara " });

        Assert.Equal(1, _upstream.Calls);
        Assert.Contains("q=mascara&", _upstream.LastQuery);
    }

    [Fact]
    public async Task A_failing_database_cache_falls_back_to_DummyJSON()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache, ThrowingCache>();
        services.AddHybridCache();
        var client = new DummyJsonProductClient(
            new HttpClient(_upstream) { BaseAddress = new Uri("https://dummyjson.test/") },
            services.BuildServiceProvider().GetRequiredService<HybridCache>(),
            Options.Create(new DummyJsonOptions()),
            NullLogger<DummyJsonProductClient>.Instance);

        var product = await client.GetProductByIdAsync(1);

        Assert.Equal("Essence Mascara", product!.Title);
    }

    private sealed class ThrowingCache : IDistributedCache
    {
        private static InvalidOperationException Down() => new("database unreachable");
        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
    }

    [Fact]
    public async Task CacheMinutes_zero_disables_caching()
    {
        var client = NewInstance(cacheMinutes: 0);

        await client.GetProductByIdAsync(1);
        await client.GetProductByIdAsync(1);

        Assert.Equal(2, _upstream.Calls);
    }

    private sealed class SharedTableStandIn : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _rows = new();
        public int Count => _rows.Count;

        public byte[]? Get(string key) => _rows.GetValueOrDefault(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _rows[key] = value;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _rows.TryRemove(key, out _);
        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _calls;
        public int Calls => _calls;
        public string LastQuery { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            var path = request.RequestUri!.AbsolutePath;
            LastQuery = request.RequestUri.Query;

            var response = path switch
            {
                "/products/1" => Json(ProductJson),
                "/products" or "/products/search" => Json(ListJson),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
            return Task.FromResult(response);
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
