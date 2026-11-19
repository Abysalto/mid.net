using AbySalto.Mid.Domain.Enums;
using AbySalto.Mid.Domain.Exceptions;

namespace AbySalto.Mid.Domain.Entities;

public class BasketItem
{
    public int Id { get; private set; }
    public int BasketId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductTitle { get; private set; } = string.Empty;
    public string? Thumbnail { get; private set; }
    public ProductCategory Category { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public DateTime AddedAt { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    private BasketItem() { }

    internal BasketItem(
        int productId, string title, string? thumbnail, ProductCategory category, decimal unitPrice, int quantity,
        int maxQuantity, DateTime addedAt)
    {
        if (productId <= 0) throw new DomainException("Product id must be positive.");
        if (unitPrice < 0) throw new DomainException("Unit price cannot be negative.");
        EnsureValidQuantity(quantity, maxQuantity);

        ProductId = productId;
        ProductTitle = ProductSnapshot.Title(title);
        Thumbnail = ProductSnapshot.Thumbnail(thumbnail);
        Category = category;
        UnitPrice = unitPrice;
        Quantity = quantity;
        AddedAt = addedAt;
    }

    internal void IncreaseQuantity(int by, int maxQuantity)
    {
        if (by < 1) throw new DomainException("Quantity to add must be at least 1.");
        EnsureValidQuantity(Quantity + by, maxQuantity);
        Quantity += by;
    }

    internal void SetQuantity(int quantity, int maxQuantity)
    {
        EnsureValidQuantity(quantity, maxQuantity);
        Quantity = quantity;
    }

    private static void EnsureValidQuantity(int quantity, int maxQuantity)
    {
        if (quantity < 1 || quantity > maxQuantity)
            throw new DomainException(
                $"Quantity must be between 1 and {maxQuantity}.", "basket.invalid_quantity",
                new Dictionary<string, object?> { ["requested"] = quantity, ["maxQuantity"] = maxQuantity });
    }
}
