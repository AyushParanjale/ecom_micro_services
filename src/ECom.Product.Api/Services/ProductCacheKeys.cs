namespace ECom.Product.Api.Services;

using System.Globalization;
using ECom.Product.Api.DTOs;

public static class ProductCacheKeys
{
    public const string VersionKey = "products:version";
    private const int MaxCategoryLength = 100;

    // Every query dimension is part of the key so distinct filters/pages never share an entry.
    // Search and category are bounded to 100 chars by ProductQuery validation.
    public static string List(string version, ProductQuery query)
    {
        // Escaped so free text containing ':' or '=' cannot collide with another query's key.
        var search = Uri.EscapeDataString(NormalizeText(query.Search) ?? string.Empty);
        var category = Uri.EscapeDataString(NormalizeCategory(query.Category) ?? string.Empty);
        var min = query.MinPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var max = query.MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var sort = query.SortByPrice ? "price" : "name";
        var direction = query.SortDescending ? "desc" : "asc";

        return $"products:list:{version}:c={category}:q={search}:min={min}:max={max}:s={sort}:{direction}:p={query.Page}:ps={query.PageSize}";
    }

    public static string Categories(string version) =>
        $"products:categories:{version}";

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

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeCategory(value);
}
