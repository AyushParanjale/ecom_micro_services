namespace ECom.Product.Api.Options;

public sealed record CachingOptions
{
    public const string SectionName = "Caching";

    public int ProductListExpirationMinutes { get; init; } = 5;
    public int ProductDetailExpirationMinutes { get; init; } = 10;
}
