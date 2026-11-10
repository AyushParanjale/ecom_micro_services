namespace ECom.Product.Api.DTOs;

using System.ComponentModel.DataAnnotations;

/// <summary>Query-string filters, sorting, and paging for <c>GET /api/products</c>.</summary>
public sealed record ProductQuery : IValidatableObject
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 100;

    /// <summary>Case-insensitive match against name, description, or category.</summary>
    [MaxLength(100, ErrorMessage = "Search must not exceed 100 characters.")]
    public string? Search { get; init; }

    /// <summary>Case-insensitive exact category match.</summary>
    [MaxLength(100, ErrorMessage = "Category must not exceed 100 characters.")]
    public string? Category { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "MinPrice cannot be negative.")]
    public decimal? MinPrice { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "MaxPrice cannot be negative.")]
    public decimal? MaxPrice { get; init; }

    /// <summary><c>name</c> (default) or <c>price</c>.</summary>
    [RegularExpression("^(?i:name|price)$", ErrorMessage = "SortBy must be 'name' or 'price'.")]
    public string? SortBy { get; init; }

    /// <summary><c>asc</c> (default) or <c>desc</c>.</summary>
    [RegularExpression("^(?i:asc|desc)$", ErrorMessage = "SortDirection must be 'asc' or 'desc'.")]
    public string? SortDirection { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; init; } = DefaultPageSize;

    public bool SortByPrice => string.Equals(SortBy, "price", StringComparison.OrdinalIgnoreCase);

    public bool SortDescending => string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice is not null && MaxPrice is not null && MinPrice > MaxPrice)
            yield return new ValidationResult(
                "MinPrice cannot be greater than MaxPrice.",
                [nameof(MinPrice)]);
    }
}
