namespace ECom.Order.Api.DTOs;

public sealed record OrderStatusChangedNotification(
    Guid OrderId,
    Guid UserId,
    string Status,
    DateTimeOffset OccurredAt);
