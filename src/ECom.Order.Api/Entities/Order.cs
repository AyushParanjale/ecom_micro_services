namespace ECom.Order.Api.Entities;

using ECom.Shared.Exceptions;

public sealed class Order
{
    private List<OrderItem> _items = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset OrderDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public byte[]? RowVersion { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public Order(Guid userId, decimal totalAmount)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        if (totalAmount < 0)
            throw new DomainException("Order total amount cannot be negative.");

        Id = Guid.NewGuid();
        UserId = userId;
        OrderDate = DateTimeOffset.UtcNow;
        TotalAmount = totalAmount;
        Status = OrderStatus.PendingPayment;
    }

    public void AddItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        var item = new OrderItem(Id, productId, productName, unitPrice, quantity);
        _items.Add(item);
    }

    public void ConfirmPayment()
    {
        if (Status != OrderStatus.PendingPayment)
            throw new DomainException("Only PendingPayment orders can be confirmed.");
        Status = OrderStatus.Confirmed;
    }

    public void MarkPaymentFailed()
    {
        if (Status != OrderStatus.PendingPayment)
            throw new DomainException("Only PendingPayment orders can be marked as PaymentFailed.");
        Status = OrderStatus.PaymentFailed;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Cancelled)
            throw new DomainException("Order is already cancelled.");
        Status = OrderStatus.Cancelled;
    }
}
