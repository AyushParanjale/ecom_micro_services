namespace ECom.Order.Api.Messaging;

public static class OutboxEventTypes
{
    /// <summary>Pushed to the order owner over SignalR.</summary>
    public const string OrderStatusChanged = "OrderStatusChanged";

    /// <summary>Published to the <c>OrderCreated</c> topic (Cart clears the checked-out lines).</summary>
    public const string OrderCreated = "OrderCreated";

    /// <summary>Published to the <c>OrderPaymentFailed</c> topic (Product restores stock).</summary>
    public const string OrderPaymentFailed = "OrderPaymentFailed";
}

public sealed record OrderLineItem(Guid ProductId, int Quantity);

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid UserId,
    IReadOnlyList<OrderLineItem> Items,
    DateTimeOffset OccurredAt);

public sealed record OrderPaymentFailedEvent(
    Guid OrderId,
    Guid UserId,
    IReadOnlyList<OrderLineItem> Items,
    DateTimeOffset OccurredAt);
