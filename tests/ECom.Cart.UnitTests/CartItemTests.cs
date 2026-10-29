namespace ECom.Cart.UnitTests;

using ECom.Cart.Api.Entities;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class CartItemTests
{
    private static readonly Guid CartId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    [TestMethod]
    public void CreateCartItem_ValidArguments_Succeeds()
    {
        var item = new CartItem(CartId, ProductId, 3);

        Assert.AreEqual(CartId, item.CartId);
        Assert.AreEqual(ProductId, item.ProductId);
        Assert.AreEqual(3, item.Quantity);
        Assert.AreNotEqual(Guid.Empty, item.Id);
    }

    [TestMethod]
    public void CreateCartItem_ZeroQuantity_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new CartItem(CartId, ProductId, 0));
    }

    [TestMethod]
    public void CreateCartItem_NegativeQuantity_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new CartItem(CartId, ProductId, -1));
    }

    [TestMethod]
    public void CreateCartItem_ValidQuantity_AcceptsPositiveValues()
    {
        var item = new CartItem(CartId, ProductId, 100);

        Assert.AreEqual(100, item.Quantity);
    }

    [TestMethod]
    public void UpdateQuantity_ValidQuantity_UpdatesValue()
    {
        var item = new CartItem(CartId, ProductId, 1);

        item.UpdateQuantity(5);

        Assert.AreEqual(5, item.Quantity);
    }

    [TestMethod]
    public void UpdateQuantity_ZeroQuantity_ThrowsDomainException()
    {
        var item = new CartItem(CartId, ProductId, 1);

        Assert.ThrowsExactly<DomainException>(() => item.UpdateQuantity(0));
    }

    [TestMethod]
    public void UpdateQuantity_NegativeQuantity_ThrowsDomainException()
    {
        var item = new CartItem(CartId, ProductId, 1);

        Assert.ThrowsExactly<DomainException>(() => item.UpdateQuantity(-5));
    }

    [TestMethod]
    public void CreateCartItem_EmptyCartId_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CartItem(Guid.Empty, ProductId, 1));
    }

    [TestMethod]
    public void CreateCartItem_EmptyProductId_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CartItem(CartId, Guid.Empty, 1));
    }
}
