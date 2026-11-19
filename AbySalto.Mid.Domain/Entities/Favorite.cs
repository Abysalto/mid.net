using AbySalto.Mid.Domain.Enums;

namespace AbySalto.Mid.Domain.Entities;

public class Favorite
{
    public Guid UserId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductTitle { get; private set; } = string.Empty;
    public string? Thumbnail { get; private set; }
    public ProductCategory Category { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Favorite() { }

    public Favorite(Guid userId, int productId, string productTitle, string? thumbnail, ProductCategory category, DateTime createdAt)
    {
        UserId = userId;
        ProductId = productId;
        ProductTitle = productTitle;
        Thumbnail = thumbnail;
        Category = category;
        CreatedAt = createdAt;
    }
}
