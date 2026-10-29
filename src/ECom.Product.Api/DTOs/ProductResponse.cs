namespace ECom.Product.Api.DTOs;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Category,
    int StockQuantity,
    string? ImageUrl);
