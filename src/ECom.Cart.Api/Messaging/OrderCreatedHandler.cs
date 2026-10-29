namespace ECom.Cart.Api.Messaging;

using ECom.Cart.Api.Services;
using ECom.Shared.Messaging;

// Local copy of the Order service's integration event contract (topic: OrderCreated).
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid UserId,
    IReadOnlyList<OrderCreatedItem> Items,
    DateTimeOffset OccurredAt);

public sealed record OrderCreatedItem(Guid ProductId, int Quantity);

/// <summary>Removes the checked-out lines from the user's cart once the order exists.</summary>
public sealed class OrderCreatedHandler : IIntegrationEventHandler<OrderCreatedEvent>
{
    private readonly ICartService _cartService;
    private readonly ILogger<OrderCreatedHandler> _logger;

    public OrderCreatedHandler(ICartService cartService, ILogger<OrderCreatedHandler> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent message, string messageId, CancellationToken cancellationToken = default)
    {
        var removed = await _cartService.RemoveCheckedOutItemsAsync(
            message.UserId,
            message.Items.Select(i => i.ProductId),
            cancellationToken);

        _logger.LogInformation(
            "Handled OrderCreated MessageId={MessageId} OrderId={OrderId}: removed {Count} cart items",
            messageId, message.OrderId, removed);
    }
}
