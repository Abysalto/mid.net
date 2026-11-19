using AbySalto.Mid.Application.Baskets.Dtos;
using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Application.Products;
using AbySalto.Mid.Application.Products.Dtos;
using AbySalto.Mid.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AbySalto.Mid.Application.Baskets;

internal sealed class BasketService(
    IAppDbContext db,
    IProductClient productClient,
    ICurrentUser currentUser,
    IOptions<BasketOptions> options,
    TimeProvider timeProvider) : IBasketService
{
    private readonly BasketLimits _limits = options.Value.ToLimits();

    public async Task<BasketDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        var basket = await db.Baskets.AsNoTracking()
            .Include(b => b.Items)
            .SingleOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        return basket is null ? Empty : ToDto(basket);
    }

    public async Task<BasketDto> AddItemAsync(AddBasketItemRequest request, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateProductId(request.ProductId);
        ValidateQuantityToAdd(request.Quantity);

        var product = await GetProductAsync(request.ProductId, cancellationToken);

        var basket = await LoadBasketAsync(userId, cancellationToken);
        if (basket is null)
        {
            basket = new Basket(userId, Now);
            db.Baskets.Add(basket);
        }

        var alreadyInBasket = basket.Items.FirstOrDefault(i => i.ProductId == product.Id)?.Quantity ?? 0;
        EnsureStockForAdd(product, request.Quantity, alreadyInBasket);

        basket.AddItem(product.Id, product.Title, product.Thumbnail, product.Category, product.Price, request.Quantity, _limits, Now);

        await SaveAsync(cancellationToken);
        return ToDto(basket);
    }

    public async Task<BasketDto> UpdateItemQuantityAsync(
        int productId, UpdateBasketItemQuantityRequest request, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateProductId(productId);
        ValidateQuantityToSet(productId, request.Quantity);

        var basket = await LoadBasketWithItemAsync(userId, productId, cancellationToken);

        var product = await GetProductAsync(productId, cancellationToken);
        EnsureStockForSet(product, request.Quantity);

        basket.SetItemQuantity(productId, request.Quantity, _limits, Now);

        await SaveAsync(cancellationToken);
        return ToDto(basket);
    }

    public async Task<BasketDto> RemoveItemAsync(int productId, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        ValidateProductId(productId);

        var basket = await LoadBasketWithItemAsync(userId, productId, cancellationToken);
        basket.RemoveItem(productId, Now);

        await SaveAsync(cancellationToken);
        return ToDto(basket);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        var basket = await LoadBasketAsync(userId, cancellationToken);
        if (basket is null || basket.Items.Count == 0)
            return;

        basket.Clear(Now);
        await SaveAsync(cancellationToken);
    }

    private static readonly BasketDto Empty = new([], 0, 0m);

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private Guid RequireUserId() => currentUser.UserId ?? throw new AuthenticationFailedException();

    private Task<Basket?> LoadBasketAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Baskets.Include(b => b.Items).SingleOrDefaultAsync(b => b.UserId == userId, cancellationToken);

    private async Task<Basket> LoadBasketWithItemAsync(Guid userId, int productId, CancellationToken cancellationToken)
    {
        var basket = await LoadBasketAsync(userId, cancellationToken);
        if (basket is null || basket.Items.All(i => i.ProductId != productId))
            throw new NotFoundException(
                $"Product {productId} is not in your basket. Add it with POST /api/basket/items first.",
                "basket.item_not_found", Details(("productId", productId)));
        return basket;
    }

    private async Task<ProductDto> GetProductAsync(int productId, CancellationToken cancellationToken) =>
        await productClient.GetProductByIdAsync(productId, cancellationToken)
        ?? throw new NotFoundException($"Product {productId} does not exist.", "product.not_found", Details(("productId", productId)));

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException(
                "Your basket was changed by another request at the same time. Reload it and try again.", ex, "basket.concurrent_update");
        }
        catch (DbUpdateException ex)
        {
            // With product text fitted to the columns (ProductSnapshot), the remaining cause is a unique index:
            // two first adds both creating the basket, or the same product added twice at once.
            throw new ConflictException(
                "Your basket was changed by another request at the same time. Try again.", ex, "basket.concurrent_update");
        }
    }

    private static void ValidateProductId(int productId)
    {
        if (productId <= 0)
            throw new ValidationException("Product id must be a positive number.", "product.invalid_id", Details(("productId", productId)));
    }

    private void ValidateQuantityToAdd(int quantity)
    {
        if (quantity < 1)
            throw new ValidationException(
                "Quantity to add must be at least 1.", "basket.invalid_quantity",
                Details(("requested", quantity), ("maxQuantity", _limits.MaxQuantity)));

        if (quantity > _limits.MaxQuantity)
            throw new ValidationException(
                $"You can't add more than {_limits.MaxQuantity} of one product.", "basket.quantity_limit_exceeded",
                Details(("requested", quantity), ("maxQuantity", _limits.MaxQuantity), ("maxAddable", _limits.MaxQuantity)));
    }

    private void ValidateQuantityToSet(int productId, int quantity)
    {
        if (quantity < 1)
            throw new ValidationException(
                $"Quantity must be at least 1. To remove the product, use DELETE /api/basket/items/{productId}.",
                "basket.invalid_quantity", Details(("productId", productId), ("requested", quantity)));

        if (quantity > _limits.MaxQuantity)
            throw new ValidationException(
                $"The maximum per product is {_limits.MaxQuantity}; you asked for {quantity}.", "basket.quantity_limit_exceeded",
                Details(("productId", productId), ("requested", quantity), ("maxQuantity", _limits.MaxQuantity)));
    }

    private static void EnsureStockForAdd(ProductDto product, int requested, int inBasket)
    {
        if (inBasket + requested <= product.Stock)
            return;

        if (product.Stock == 0)
            throw OutOfStock(product);

        var canAdd = Math.Max(product.Stock - inBasket, 0);
        var message = inBasket == 0
            ? $"Only {product.Stock} of '{product.Title}' are in stock; you asked for {requested}."
            : $"Only {product.Stock} of '{product.Title}' are in stock and you already have {inBasket} in your basket, " +
              (canAdd > 0 ? $"so you can add at most {canAdd} more." : "so you can't add any more.");

        throw new ValidationException(message, "basket.insufficient_stock", Details(
            ("productId", product.Id), ("requested", requested), ("inStock", product.Stock),
            ("inBasket", inBasket), ("maxAddable", canAdd)));
    }

    private static void EnsureStockForSet(ProductDto product, int requested)
    {
        if (requested <= product.Stock)
            return;

        if (product.Stock == 0)
            throw OutOfStock(product);

        throw new ValidationException(
            $"Only {product.Stock} of '{product.Title}' are in stock, so the quantity can't be set to {requested}.",
            "basket.insufficient_stock",
            Details(("productId", product.Id), ("requested", requested), ("inStock", product.Stock)));
    }

    private static ValidationException OutOfStock(ProductDto product) =>
        new($"'{product.Title}' is out of stock.", "basket.out_of_stock",
            Details(("productId", product.Id), ("inStock", 0)));

    private static Dictionary<string, object?> Details(params (string Key, object? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value);

    private static BasketDto ToDto(Basket basket) => new(
        basket.Items
            .OrderBy(i => i.AddedAt).ThenBy(i => i.ProductId)
            .Select(i => new BasketItemDto(i.ProductId, i.ProductTitle, i.Thumbnail, i.Category, i.Quantity, i.UnitPrice, i.LineTotal))
            .ToList(),
        basket.TotalItems,
        basket.TotalPrice);
}
