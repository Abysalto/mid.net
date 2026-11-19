using AbySalto.Mid.Application.Baskets.Dtos;

namespace AbySalto.Mid.Application.Baskets;

public interface IBasketService
{
    Task<BasketDto> GetAsync(CancellationToken cancellationToken = default);

    Task<BasketDto> AddItemAsync(AddBasketItemRequest request, CancellationToken cancellationToken = default);

    Task<BasketDto> UpdateItemQuantityAsync(int productId, UpdateBasketItemQuantityRequest request, CancellationToken cancellationToken = default);

    Task<BasketDto> RemoveItemAsync(int productId, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
