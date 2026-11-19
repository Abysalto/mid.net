namespace AbySalto.Mid.Application.Baskets.Dtos;

public record BasketDto(IReadOnlyList<BasketItemDto> Items, int TotalItems, decimal TotalPrice);
