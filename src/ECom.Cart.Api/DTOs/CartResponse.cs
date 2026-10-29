namespace ECom.Cart.Api.DTOs;

public sealed record CartResponse(
    Guid Id,
    Guid UserId,
    IReadOnlyList<CartItemResponse> Items,
    int TotalItems,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
