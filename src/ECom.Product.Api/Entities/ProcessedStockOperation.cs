namespace ECom.Product.Api.Entities;

public enum StockOperationType
{
    Decrement = 0,
    Restore = 1
}

/// <summary>
/// Idempotency record: one row per (OrderId, Operation). Guarantees a checkout's stock decrement
/// and its compensating restore are each applied at most once, even when requests or
/// Service Bus messages are delivered more than once.
/// </summary>
public sealed class ProcessedStockOperation
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public StockOperationType Operation { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    private ProcessedStockOperation() { }

    public ProcessedStockOperation(Guid orderId, StockOperationType operation, DateTimeOffset processedAt)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId must not be empty.", nameof(orderId));

        Id = Guid.NewGuid();
        OrderId = orderId;
        Operation = operation;
        ProcessedAt = processedAt;
    }
}
