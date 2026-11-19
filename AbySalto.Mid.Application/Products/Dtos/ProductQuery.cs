using AbySalto.Mid.Domain.Enums;

namespace AbySalto.Mid.Application.Products.Dtos;

public record ProductQuery
{
    public const int DefaultPageSize = 30;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    public string? Search { get; init; }

    public ProductCategory? Category { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    public string? SortBy { get; init; }
    public string? Order { get; init; }

    public int Skip() => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);
}
