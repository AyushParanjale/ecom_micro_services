namespace ECom.Product.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public sealed record CreateProductRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MinLength(1, ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name must not exceed 200 characters.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [MinLength(1, ErrorMessage = "Description is required.")]
    [MaxLength(2000, ErrorMessage = "Description must not exceed 2000 characters.")]
    public string Description { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Price must be greater than zero.")]
    public decimal Price { get; init; }

    [Required(ErrorMessage = "Category is required.")]
    [MinLength(1, ErrorMessage = "Category is required.")]
    [MaxLength(100, ErrorMessage = "Category must not exceed 100 characters.")]
    public string Category { get; init; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "StockQuantity cannot be negative.")]
    public int StockQuantity { get; init; }

    [MaxLength(500, ErrorMessage = "ImageUrl must not exceed 500 characters.")]
    [Url(ErrorMessage = "ImageUrl must be a valid absolute HTTP or HTTPS URL.")]
    public string? ImageUrl { get; init; }
}
