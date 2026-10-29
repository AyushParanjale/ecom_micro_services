namespace ECom.Order.Api.DTOs;

using ECom.Order.Api.Entities;


public sealed record OrderResponse(
    Guid Id,
    DateTimeOffset OrderDate,
    decimal TotalAmount,
    OrderStatus Status,
    IReadOnlyList<OrderItemResponse> Items);
