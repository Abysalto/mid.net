using AbySalto.Mid.Application.Favorites.Dtos;

namespace AbySalto.Mid.Application.Favorites;

public interface IFavoriteService
{
    Task<IReadOnlyList<FavoriteDto>> GetAsync(CancellationToken cancellationToken = default);

    Task<FavoriteDto> AddAsync(int productId, CancellationToken cancellationToken = default);

    Task RemoveAsync(int productId, CancellationToken cancellationToken = default);
}
