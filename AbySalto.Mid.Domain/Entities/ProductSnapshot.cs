namespace AbySalto.Mid.Domain.Entities;

// Basket lines and favorites keep a copy of the product's title and thumbnail. That data comes from DummyJSON,
// not from the user, so instead of rejecting a value that doesn't fit the column, it is made to fit:
// otherwise the save fails and the user gets a misleading error for something they didn't send.
public static class ProductSnapshot
{
    public const int MaxTitleLength = 256;
    public const int MaxThumbnailLength = 1024;

    public static string Title(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        return trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..(MaxTitleLength - 1)] + "…";
    }

    // A cut-off URL is a broken image, so an oversized one is dropped instead.
    public static string? Thumbnail(string? thumbnail) =>
        thumbnail is { Length: > 0 and <= MaxThumbnailLength } ? thumbnail : null;
}
