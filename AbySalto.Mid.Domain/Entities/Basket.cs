using AbySalto.Mid.Domain.Enums;
using AbySalto.Mid.Domain.Exceptions;

namespace AbySalto.Mid.Domain.Entities;

public class Basket
{
    private readonly List<BasketItem> _items = [];

    public int Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<BasketItem> Items => _items.AsReadOnly();

    private Basket() { }

    public Basket(Guid userId, DateTime now)
    {
        UserId = userId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public int TotalItems => _items.Sum(i => i.Quantity);
    public decimal TotalPrice => _items.Sum(i => i.LineTotal);

    public BasketItem AddItem(
        int productId, string title, string? thumbnail, ProductCategory category, decimal unitPrice, int quantity,
        BasketLimits limits, DateTime now)
    {
        var existing = FindItem(productId);
        if (existing is not null)
        {
            var total = existing.Quantity + quantity;
            if (total > limits.MaxQuantity)
            {
                var canAdd = limits.MaxQuantity - existing.Quantity;
                throw new DomainException(
                    $"'{existing.ProductTitle}' is already in your basket {existing.Quantity} time(s). Adding {quantity} would make {total}, " +
                    $"but the maximum per product is {limits.MaxQuantity}. " +
                    (canAdd > 0 ? $"You can add at most {canAdd} more." : "You can't add any more of it."),
                    "basket.quantity_limit_exceeded",
                    new Dictionary<string, object?>
                    {
                        ["productId"] = productId, ["requested"] = quantity, ["inBasket"] = existing.Quantity,
                        ["maxQuantity"] = limits.MaxQuantity, ["maxAddable"] = Math.Max(canAdd, 0),
                    });
            }

            existing.IncreaseQuantity(quantity, limits.MaxQuantity);
        }
        else
        {
            if (_items.Count >= limits.MaxDistinctProducts)
                throw new DomainException(
                    $"Your basket already has {_items.Count} different products, which is the maximum. " +
                    $"Remove one before adding '{title}'.",
                    "basket.product_limit_reached",
                    new Dictionary<string, object?>
                    {
                        ["productId"] = productId, ["distinctProducts"] = _items.Count,
                        ["maxDistinctProducts"] = limits.MaxDistinctProducts,
                    });

            existing = new BasketItem(productId, title, thumbnail, category, unitPrice, quantity, limits.MaxQuantity, now);
            _items.Add(existing);
        }

        UpdatedAt = now;
        return existing;
    }

    public void SetItemQuantity(int productId, int quantity, BasketLimits limits, DateTime now)
    {
        var item = FindItem(productId) ?? throw new DomainException($"Product {productId} is not in the basket.");
        item.SetQuantity(quantity, limits.MaxQuantity);
        UpdatedAt = now;
    }

    public void RemoveItem(int productId, DateTime now)
    {
        var item = FindItem(productId) ?? throw new DomainException($"Product {productId} is not in the basket.");
        _items.Remove(item);
        UpdatedAt = now;
    }

    public void Clear(DateTime now)
    {
        _items.Clear();
        UpdatedAt = now;
    }

    private BasketItem? FindItem(int productId) => _items.FirstOrDefault(i => i.ProductId == productId);
}
