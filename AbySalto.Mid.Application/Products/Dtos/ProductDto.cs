using AbySalto.Mid.Domain.Enums;

namespace AbySalto.Mid.Application.Products.Dtos;

public record ProductDto(
    int Id,
    string Title,
    string Description,
    ProductCategory Category,
    string? Brand,
    decimal Price,
    decimal DiscountPercentage,
    decimal Rating,
    int Stock,
    string? Thumbnail,
    IReadOnlyList<string> Images);
