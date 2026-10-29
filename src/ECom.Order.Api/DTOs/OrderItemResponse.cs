namespace ECom.Order.Api.DTOs;

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
