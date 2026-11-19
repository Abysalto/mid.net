using AbySalto.Mid.Domain.Enums;

namespace AbySalto.Mid.Application.Favorites.Dtos;

public record FavoriteDto(int ProductId, string Title, string? Thumbnail, ProductCategory Category, DateTime AddedAt);
