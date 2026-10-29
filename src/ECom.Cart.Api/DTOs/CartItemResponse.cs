namespace ECom.Cart.Api.DTOs;

public sealed record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string? ImageUrl,
    string Category,
    int Quantity,
    int AvailableStock,
    decimal CurrentUnitPrice,
    decimal LineTotal);
