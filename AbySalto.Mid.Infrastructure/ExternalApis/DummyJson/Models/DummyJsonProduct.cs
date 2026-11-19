namespace AbySalto.Mid.Infrastructure.ExternalApis.DummyJson.Models;

internal sealed record DummyJsonProduct(
    int Id,
    string Title,
    string? Description,
    string? Category,
    string? Brand,
    decimal Price,
    decimal DiscountPercentage,
    decimal Rating,
    int Stock,
    string? Thumbnail,
    List<string>? Images);

internal sealed record DummyJsonProductsResponse(List<DummyJsonProduct> Products, int Total, int Skip, int Limit);