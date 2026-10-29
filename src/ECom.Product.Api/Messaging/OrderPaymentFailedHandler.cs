namespace ECom.Product.Api.Messaging;

using ECom.Product.Api.DTOs;
using ECom.Product.Api.Services;
using ECom.Shared.Messaging;

// Local copy of the Order service's integration event contract (topic: OrderPaymentFailed).
public sealed record OrderPaymentFailedEvent(
    Guid OrderId,
    Guid UserId,
    IReadOnlyList<OrderPaymentFailedItem> Items,
    DateTimeOffset OccurredAt);

public sealed record OrderPaymentFailedItem(Guid ProductId, int Quantity);

/// <summary>Restores the stock taken at checkout when the order's payment is declined.</summary>
public sealed class OrderPaymentFailedHandler : IIntegrationEventHandler<OrderPaymentFailedEvent>
{
    private readonly IStockService _stockService;
    private readonly ILogger<OrderPaymentFailedHandler> _logger;

    public OrderPaymentFailedHandler(IStockService stockService, ILogger<OrderPaymentFailedHandler> logger)
    {
        _stockService = stockService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderPaymentFailedEvent message, string messageId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling OrderPaymentFailed MessageId={MessageId} OrderId={OrderId}", messageId, message.OrderId);

        var lines = message.Items.Select(i => new StockLine(i.ProductId, i.Quantity)).ToList();
        await _stockService.RestoreAsync(message.OrderId, lines, cancellationToken);
    }
}
