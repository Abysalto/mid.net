using AbySalto.Mid.Domain.Enums;

namespace AbySalto.Mid.Application.Baskets.Dtos;

public record BasketItemDto(
    int ProductId, string Title, string? Thumbnail, ProductCategory Category, int Quantity, decimal UnitPrice, decimal LineTotal);
