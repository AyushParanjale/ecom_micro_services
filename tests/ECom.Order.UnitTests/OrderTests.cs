namespace ECom.Order.UnitTests;

using ECom.Order.Api.Entities;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class OrderTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [TestMethod]
    public void CreateOrder_ValidArguments_Succeeds()
    {
        var order = new Order(UserId, 99.99m);

        Assert.AreEqual(UserId, order.UserId);
        Assert.AreEqual(99.99m, order.TotalAmount);
        Assert.AreEqual(OrderStatus.PendingPayment, order.Status);
        Assert.AreNotEqual(Guid.Empty, order.Id);
    }

    [TestMethod]
    public void CreateOrder_NegativeTotalAmount_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Order(UserId, -0.01m));
    }

    [TestMethod]
    public void CreateOrder_ZeroTotalAmount_Succeeds()
    {
        var order = new Order(UserId, 0m);

        Assert.AreEqual(0m, order.TotalAmount);
    }

    [TestMethod]
    public void CreateOrder_InitialStatus_IsPendingPayment()
    {
        var order = new Order(UserId, 10m);

        Assert.AreEqual(OrderStatus.PendingPayment, order.Status);
    }

    [TestMethod]
    public void CreateOrder_OrderDate_IsDateTimeOffset()
    {
        var before = DateTimeOffset.UtcNow;
        var order = new Order(UserId, 10m);
        var after = DateTimeOffset.UtcNow;

        Assert.IsTrue(order.OrderDate >= before);
        Assert.IsTrue(order.OrderDate <= after);
    }

    [TestMethod]
    public void CreateOrder_OrderDate_IsInUtc()
    {
        var order = new Order(UserId, 10m);

        Assert.AreEqual(TimeSpan.Zero, order.OrderDate.Offset);
    }

    [TestMethod]
    public void ConfirmPayment_PendingPaymentOrder_SetsStatusToConfirmed()
    {
        var order = new Order(UserId, 10m);

        order.ConfirmPayment();

        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
    }

    [TestMethod]
    public void ConfirmPayment_AlreadyConfirmedOrder_ThrowsDomainException()
    {
        var order = new Order(UserId, 10m);
        order.ConfirmPayment();

        Assert.ThrowsExactly<DomainException>(() => order.ConfirmPayment());
    }

    [TestMethod]
    public void MarkPaymentFailed_PendingPaymentOrder_SetsStatusToPaymentFailed()
    {
        var order = new Order(UserId, 10m);

        order.MarkPaymentFailed();

        Assert.AreEqual(OrderStatus.PaymentFailed, order.Status);
    }

    [TestMethod]
    public void MarkPaymentFailed_ConfirmedOrder_ThrowsDomainException()
    {
        var order = new Order(UserId, 10m);
        order.ConfirmPayment();

        Assert.ThrowsExactly<DomainException>(() => order.MarkPaymentFailed());
    }

    [TestMethod]
    public void MarkPaymentFailed_AlreadyPaymentFailedOrder_ThrowsDomainException()
    {
        var order = new Order(UserId, 10m);
        order.MarkPaymentFailed();

        Assert.ThrowsExactly<DomainException>(() => order.MarkPaymentFailed());
    }

    [TestMethod]
    public void ConfirmPayment_PaymentFailedOrder_ThrowsDomainException()
    {
        var order = new Order(UserId, 10m);
        order.MarkPaymentFailed();

        Assert.ThrowsExactly<DomainException>(() => order.ConfirmPayment());
    }

    [TestMethod]
    public void Cancel_PendingPaymentOrder_SetsStatusToCancelled()
    {
        var order = new Order(UserId, 10m);

        order.Cancel();

        Assert.AreEqual(OrderStatus.Cancelled, order.Status);
    }

    [TestMethod]
    public void Cancel_AlreadyCancelledOrder_ThrowsDomainException()
    {
        var order = new Order(UserId, 10m);
        order.Cancel();

        Assert.ThrowsExactly<DomainException>(() => order.Cancel());
    }

    [TestMethod]
    public void CreateOrder_EmptyUserId_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new Order(Guid.Empty, 10m));
    }
}
