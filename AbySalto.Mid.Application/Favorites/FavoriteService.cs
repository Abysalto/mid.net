using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Application.Favorites.Dtos;
using AbySalto.Mid.Application.Products;
using AbySalto.Mid.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbySalto.Mid.Application.Favorites;

internal sealed class FavoriteService(
    IAppDbContext db,
    IProductClient productClient,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IFavoriteService
{
    public async Task<IReadOnlyList<FavoriteDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        return await db.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt).ThenBy(f => f.ProductId)
            .Select(f => new FavoriteDto(f.ProductId, f.ProductTitle, f.Thumbnail, f.Category, f.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<FavoriteDto> AddAsync(int productId, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateProductId(productId);

        var existing = await FindAsync(userId, productId, cancellationToken);
        if (existing is not null)
            return ToDto(existing);

        var product = await productClient.GetProductByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException($"Product {productId} does not exist.", "product.not_found",
                new Dictionary<string, object?> { ["productId"] = productId });

        var favorite = new Favorite(userId, product.Id, product.Title, product.Thumbnail, product.Category, timeProvider.GetUtcNow().UtcDateTime);
        db.Favorites.Add(favorite);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Favorites.Entry(favorite).State = EntityState.Detached;
            var winner = await FindAsync(userId, productId, cancellationToken);
            if (winner is null)
                throw;
            return ToDto(winner);
        }

        return ToDto(favorite);
    }

    public async Task RemoveAsync(int productId, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateProductId(productId);

        var favorite = await db.Favorites.SingleOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, cancellationToken)
            ?? throw new NotFoundException($"Product {productId} is not in your favorites.", "favorite.not_found",
                new Dictionary<string, object?> { ["productId"] = productId });

        db.Favorites.Remove(favorite);
        await db.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireUserId() => currentUser.UserId ?? throw new AuthenticationFailedException();

    private Task<Favorite?> FindAsync(Guid userId, int productId, CancellationToken cancellationToken) =>
        db.Favorites.AsNoTracking().SingleOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, cancellationToken);

    private static void ValidateProductId(int productId)
    {
        if (productId <= 0)
            throw new ValidationException("Product id must be a positive number.", "product.invalid_id",
                new Dictionary<string, object?> { ["productId"] = productId });
    }

    private static FavoriteDto ToDto(Favorite f) => new(f.ProductId, f.ProductTitle, f.Thumbnail, f.Category, f.CreatedAt);
}
