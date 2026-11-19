using AbySalto.Mid.Domain.Exceptions;

namespace AbySalto.Mid.Domain.Entities;

public sealed record BasketLimits
{
    public int MaxDistinctProducts { get; }
    public int MaxQuantity { get; }

    public BasketLimits(int maxDistinctProducts, int maxQuantity)
    {
        if (maxDistinctProducts < 1) throw new DomainException("MaxDistinctProducts must be at least 1.");
        if (maxQuantity < 1) throw new DomainException("MaxQuantity must be at least 1.");

        MaxDistinctProducts = maxDistinctProducts;
        MaxQuantity = maxQuantity;
    }
}
