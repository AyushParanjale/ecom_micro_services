namespace ECom.Cart.Api.Entities;

using ECom.Shared.Exceptions;

public sealed class CartItem
{
    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private CartItem() { }

    public CartItem(Guid cartId, Guid productId, int quantity)
    {
        if (cartId == Guid.Empty)
            throw new ArgumentException("CartId must not be empty.", nameof(cartId));
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId must not be empty.", nameof(productId));
        ValidateQuantity(quantity);

        Id = Guid.NewGuid();
        CartId = cartId;
        ProductId = productId;
        Quantity = quantity;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateQuantity(int quantity)
    {
        ValidateQuantity(quantity);
        Quantity = quantity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("CartItem quantity must be greater than zero.");
    }
}
