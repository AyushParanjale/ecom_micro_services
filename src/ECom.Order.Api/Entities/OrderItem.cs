namespace ECom.Order.Api.Entities;

using ECom.Shared.Exceptions;

public sealed class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    private OrderItem()
    {
        ProductName = string.Empty;
    }

    public OrderItem(Guid orderId, Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId must not be empty.", nameof(orderId));
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId must not be empty.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainException("Product name snapshot is required.");
        if (unitPrice <= 0)
            throw new DomainException("Unit price must be greater than zero.");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        ProductName = productName.Trim();
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}
