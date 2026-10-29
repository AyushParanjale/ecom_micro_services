namespace ECom.Product.Api.Entities;

using ECom.Shared.Exceptions;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public string Category { get; private set; }
    public int StockQuantity { get; private set; }
    public string? ImageUrl { get; private set; }
    public byte[]? RowVersion { get; private set; }

    private Product()
    {
        Name = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
    }

    public Product(string name, string description, decimal price, string category, int stockQuantity, string? imageUrl = null)
    {
        ValidateName(name);
        ValidateDescription(description);
        ValidateCategory(category);
        ValidatePrice(price);
        ValidateStockQuantity(stockQuantity);

        Id = Guid.NewGuid();
        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Category = category.Trim();
        StockQuantity = stockQuantity;
        ImageUrl = imageUrl;
    }

    public void UpdateDetails(string name, string description, decimal price, string category, string? imageUrl = null)
    {
        ValidateName(name);
        ValidateDescription(description);
        ValidateCategory(category);
        ValidatePrice(price);

        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Category = category.Trim();
        ImageUrl = imageUrl;
    }

    public void DecrementStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity to decrement must be greater than zero.");
        if (StockQuantity - quantity < 0)
            throw new DomainException("Insufficient stock: cannot reduce stock below zero.");
        StockQuantity -= quantity;
    }

    public void RestoreStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity to restore must be greater than zero.");
        checked { StockQuantity += quantity; }
    }

    public void AdjustStock(int delta)
    {
        int newQuantity = StockQuantity + delta;
        if (newQuantity < 0)
            throw new DomainException("Stock quantity cannot be negative.");
        StockQuantity = newQuantity;
    }

    public void SetStockQuantity(int quantity)
    {
        ValidateStockQuantity(quantity);
        StockQuantity = quantity;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
    }

    private static void ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Product description is required.");
    }

    private static void ValidateCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new DomainException("Product category is required.");
    }

    private static void ValidatePrice(decimal price)
    {
        if (price <= 0)
            throw new DomainException("Product price must be greater than zero.");
    }

    private static void ValidateStockQuantity(int stockQuantity)
    {
        if (stockQuantity < 0)
            throw new DomainException("Stock quantity cannot be negative.");
    }
}
