using System.Text;

namespace AbySalto.Mid.Domain.Enums;

public static class ProductCategoryExtensions
{
    public static IReadOnlyList<ProductCategory> All { get; } =
        Enum.GetValues<ProductCategory>().Where(c => c != ProductCategory.Unknown).ToArray();

    private static readonly Dictionary<ProductCategory, string> Slugs = All.ToDictionary(c => c, BuildSlug);
    private static readonly Dictionary<string, ProductCategory> BySlug =
        Slugs.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static string ToSlug(this ProductCategory category) =>
        Slugs.TryGetValue(category, out var slug) ? slug : "unknown";

    public static string ToDisplayName(this ProductCategory category) =>
        string.Join(' ', category.ToSlug().Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    public static ProductCategory FromSlug(string? slug) =>
        slug is not null && BySlug.TryGetValue(slug.Trim(), out var category) ? category : ProductCategory.Unknown;

    private static string BuildSlug(ProductCategory category)
    {
        var name = category.ToString();
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }
}
