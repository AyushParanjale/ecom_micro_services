namespace ECom.Cart.Api.Clients;

// Local copy of the Product service's ProductResponse contract.
public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Category,
    int StockQuantity,
    string? ImageUrl);
