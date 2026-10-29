namespace ECom.Cart.Api.Entities;

public sealed class Cart
{
    private List<CartItem> _items = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    private Cart() { }

    public Cart(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId must not be empty.", nameof(userId));

        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public CartItem AddItem(Guid productId, int quantity)
    {
        var item = new CartItem(Id, productId, quantity);
        _items.Add(item);
        Touch();
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} is not in this cart.");
        _items.Remove(item);
        Touch();
    }

    public void UpdateItemQuantity(Guid itemId, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} is not in this cart.");
        item.UpdateQuantity(quantity);
        Touch();
    }

    public void Clear()
    {
        _items.Clear();
        Touch();
    }

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
