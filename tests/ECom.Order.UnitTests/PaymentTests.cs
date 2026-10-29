namespace ECom.Order.UnitTests;

using ECom.Order.Api.Entities;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class PaymentTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const decimal ValidAmount = 99.99m;
    private const string ValidKey = "payment-key-123";

    // --- Constructor ---

    [TestMethod]
    public void CreatePayment_ValidArguments_StartsAsPending()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);

        Assert.AreEqual(PaymentStatus.Pending, payment.Status);
        Assert.AreEqual(OrderId, payment.OrderId);
        Assert.AreEqual(UserId, payment.UserId);
        Assert.AreEqual(ValidAmount, payment.Amount);
        Assert.AreEqual(ValidKey, payment.IdempotencyKey);
        Assert.AreNotEqual(Guid.Empty, payment.Id);
    }

    [TestMethod]
    public void CreatePayment_ZeroAmount_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Payment(OrderId, UserId, 0m, ValidKey));
    }

    [TestMethod]
    public void CreatePayment_NegativeAmount_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Payment(OrderId, UserId, -1m, ValidKey));
    }

    [TestMethod]
    public void CreatePayment_EmptyIdempotencyKey_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Payment(OrderId, UserId, ValidAmount, ""));
    }

    [TestMethod]
    public void CreatePayment_WhitespaceIdempotencyKey_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            new Payment(OrderId, UserId, ValidAmount, "   "));
    }

    [TestMethod]
    public void CreatePayment_TooLongIdempotencyKey_ThrowsDomainException()
    {
        var longKey = new string('x', 129);
        Assert.ThrowsExactly<DomainException>(() =>
            new Payment(OrderId, UserId, ValidAmount, longKey));
    }

    [TestMethod]
    public void CreatePayment_EmptyOrderId_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new Payment(Guid.Empty, UserId, ValidAmount, ValidKey));
    }

    [TestMethod]
    public void CreatePayment_EmptyUserId_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new Payment(OrderId, Guid.Empty, ValidAmount, ValidKey));
    }

    [TestMethod]
    public void CreatePayment_IdempotencyKey_IsTrimmed()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, "  key  ");

        Assert.AreEqual("key", payment.IdempotencyKey);
    }

    // --- MarkSucceeded ---

    [TestMethod]
    public void MarkSucceeded_PendingPayment_SetsStatusToSucceeded()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        var now = DateTimeOffset.UtcNow;

        payment.MarkSucceeded("fake_ref_123", now);

        Assert.AreEqual(PaymentStatus.Succeeded, payment.Status);
        Assert.AreEqual("fake_ref_123", payment.ProviderReference);
    }

    [TestMethod]
    public void MarkSucceeded_SetsUpdatedAt()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        var now = DateTimeOffset.UtcNow.AddSeconds(5);

        payment.MarkSucceeded("fake_ref_123", now);

        Assert.AreEqual(now, payment.UpdatedAt);
    }

    [TestMethod]
    public void MarkSucceeded_EmptyProviderReference_ThrowsDomainException()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);

        Assert.ThrowsExactly<DomainException>(() =>
            payment.MarkSucceeded("", DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void MarkSucceeded_AlreadySucceeded_ThrowsDomainException()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        payment.MarkSucceeded("fake_ref_123", DateTimeOffset.UtcNow);

        Assert.ThrowsExactly<DomainException>(() =>
            payment.MarkSucceeded("fake_ref_456", DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void MarkSucceeded_FailedPayment_ThrowsDomainException()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        payment.MarkFailed("declined", DateTimeOffset.UtcNow);

        Assert.ThrowsExactly<DomainException>(() =>
            payment.MarkSucceeded("fake_ref_123", DateTimeOffset.UtcNow));
    }

    // --- MarkFailed ---

    [TestMethod]
    public void MarkFailed_PendingPayment_SetsStatusToFailed()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        var now = DateTimeOffset.UtcNow;

        payment.MarkFailed("payment declined", now);

        Assert.AreEqual(PaymentStatus.Failed, payment.Status);
        Assert.AreEqual("payment declined", payment.FailureReason);
    }

    [TestMethod]
    public void MarkFailed_SetsUpdatedAt()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        var now = DateTimeOffset.UtcNow.AddSeconds(5);

        payment.MarkFailed(null, now);

        Assert.AreEqual(now, payment.UpdatedAt);
    }

    [TestMethod]
    public void MarkFailed_AlreadyFailed_ThrowsDomainException()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        payment.MarkFailed("declined", DateTimeOffset.UtcNow);

        Assert.ThrowsExactly<DomainException>(() =>
            payment.MarkFailed("declined again", DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void MarkFailed_SucceededPayment_ThrowsDomainException()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        payment.MarkSucceeded("fake_ref_123", DateTimeOffset.UtcNow);

        Assert.ThrowsExactly<DomainException>(() =>
            payment.MarkFailed("some reason", DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void MarkFailed_NullReason_Allowed()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);

        payment.MarkFailed(null, DateTimeOffset.UtcNow);

        Assert.IsNull(payment.FailureReason);
        Assert.AreEqual(PaymentStatus.Failed, payment.Status);
    }

    // --- Immutability ---

    [TestMethod]
    public void Payment_Amount_IsImmutable()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        payment.MarkSucceeded("ref", DateTimeOffset.UtcNow);

        Assert.AreEqual(ValidAmount, payment.Amount);
    }

    [TestMethod]
    public void Payment_OrderId_IsImmutable()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        payment.MarkSucceeded("ref", DateTimeOffset.UtcNow);

        Assert.AreEqual(OrderId, payment.OrderId);
    }

    [TestMethod]
    public void Payment_UserId_IsImmutable()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        payment.MarkSucceeded("ref", DateTimeOffset.UtcNow);

        Assert.AreEqual(UserId, payment.UserId);
    }

    [TestMethod]
    public void Payment_IdempotencyKey_IsImmutable()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);
        payment.MarkSucceeded("ref", DateTimeOffset.UtcNow);

        Assert.AreEqual(ValidKey, payment.IdempotencyKey);
    }

    // --- Timestamps ---

    [TestMethod]
    public void CreatePayment_CreatedAt_IsInUtc()
    {
        var payment = new Payment(OrderId, UserId, ValidAmount, ValidKey);

        Assert.AreEqual(TimeSpan.Zero, payment.CreatedAt.Offset);
    }
}
