namespace ECom.Order.UnitTests;

using ECom.Order.Api.Entities;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class OrderItemTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    [TestMethod]
    public void CreateOrderItem_ValidArguments_Succeeds()
    {
        var item = new OrderItem(OrderId, ProductId, "Widget", 19.99m, 2);

        Assert.AreEqual(OrderId, item.OrderId);
        Assert.AreEqual(ProductId, item.ProductId);
        Assert.AreEqual("Widget", item.ProductName);
        Assert.AreEqual(19.99m, item.UnitPrice);
        Assert.AreEqual(2, item.Quantity);
        Assert.AreNotEqual(Guid.Empty, item.Id);
    }

    [TestMethod]
    public void CreateOrderItem_ZeroQuantity_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new OrderItem(OrderId, ProductId, "Widget", 9.99m, 0));
    }

    [TestMethod]
    public void CreateOrderItem_NegativeQuantity_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new OrderItem(OrderId, ProductId, "Widget", 9.99m, -1));
    }

    [TestMethod]
    public void CreateOrderItem_ZeroUnitPrice_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new OrderItem(OrderId, ProductId, "Widget", 0m, 1));
    }

    [TestMethod]
    public void CreateOrderItem_NegativeUnitPrice_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new OrderItem(OrderId, ProductId, "Widget", -1m, 1));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void CreateOrderItem_EmptyProductName_ThrowsDomainException(string productName)
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new OrderItem(OrderId, ProductId, productName, 9.99m, 1));
    }

    [TestMethod]
    public void CreateOrderItem_SnapshotValuesAreRetained()
    {
        const string snapshotName = "Product At Time Of Order";
        const decimal snapshotPrice = 49.99m;

        var item = new OrderItem(OrderId, ProductId, snapshotName, snapshotPrice, 3);

        Assert.AreEqual(snapshotName, item.ProductName);
        Assert.AreEqual(snapshotPrice, item.UnitPrice);
    }

    [TestMethod]
    public void LineTotal_IsCalculatedFromUnitPriceAndQuantity()
    {
        var item = new OrderItem(OrderId, ProductId, "Widget", 10.00m, 4);

        Assert.AreEqual(40.00m, item.LineTotal);
    }
}
