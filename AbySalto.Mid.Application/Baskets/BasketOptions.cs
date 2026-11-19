using System.ComponentModel.DataAnnotations;
using AbySalto.Mid.Domain.Entities;

namespace AbySalto.Mid.Application.Baskets;

public sealed class BasketOptions
{
    public const string SectionName = "Basket";

    [Range(1, 500)]
    public int MaxDistinctProducts { get; set; } = 50;

    [Range(1, 10_000)]
    public int MaxQuantity { get; set; } = 99;

    public BasketLimits ToLimits() => new(MaxDistinctProducts, MaxQuantity);
}
