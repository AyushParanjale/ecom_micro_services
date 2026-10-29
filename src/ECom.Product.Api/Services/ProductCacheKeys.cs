namespace ECom.Product.Api.Services;

public static class ProductCacheKeys
{
    public const string VersionKey = "products:version";
    private const int MaxCategoryLength = 100;

    public static string ListAll(string version) =>
        $"products:list:{version}:all";

    public static string ListByCategory(string version, string normalizedCategory) =>
        $"products:list:{version}:{normalizedCategory}";

    public static string Detail(Guid id) =>
        $"products:detail:{id}";

    // Trim and lower-case the category; clamp to prevent unbounded keys.
    public static string? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;

        var trimmed = category.Trim();
        var lower = trimmed.ToLowerInvariant();
        return lower.Length > MaxCategoryLength ? lower[..MaxCategoryLength] : lower;
    }
}
