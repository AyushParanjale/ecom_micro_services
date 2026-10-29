namespace ECom.Order.UnitTests;

using System.Text.Json;
using ECom.Order.Api.DTOs;
using ECom.Order.Api.Entities;
using ECom.Order.Api.Messaging;
using ECom.Order.Api.Repositories;
using ECom.Order.Api.Services;
using ECom.Shared.Exceptions;
using ECom.Shared.Messaging;
using ECom.Shared.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestClass]
public sealed class PaymentServiceTests
{
    private static Order MakePendingOrder(Guid userId, decimal total = 50m)
    {
        var order = new Order(userId, total);
        order.AddItem(Guid.NewGuid(), "Widget", total, 1);
        return order;
    }

    private sealed record Mocks(
        Mock<IPaymentRepository> PaymentRepo,
        Mock<IOrderRepository> OrderRepo,
        Mock<IPaymentGateway> Gateway,
        Mock<IOutboxRepository> OutboxRepo,
        List<OutboxMessage> StagedOutbox)
    {
        public PaymentService Build() =>
            new(PaymentRepo.Object, OrderRepo.Object, Gateway.Object,
                OutboxRepo.Object, NullLogger<PaymentService>.Instance);
    }

    private static Mocks MakeMocks()
    {
        var paymentRepo = new Mock<IPaymentRepository>();
        paymentRepo.Setup(r => r.Stage(It.IsAny<Payment>()));
        paymentRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var orderRepo = new Mock<IOrderRepository>();
        var gateway = new Mock<IPaymentGateway>();

        var staged = new List<OutboxMessage>();
        var outboxRepo = new Mock<IOutboxRepository>();
        outboxRepo.Setup(r => r.Stage(It.IsAny<OutboxMessage>())).Callback<OutboxMessage>(staged.Add);

        return new(paymentRepo, orderRepo, gateway, outboxRepo, staged);
    }

    private static CreatePaymentRequest MakeRequest(Guid orderId, string key = "payment-key-001") =>
        new() { OrderId = orderId, IdempotencyKey = key };

    /// <summary>Wires a brand-new payment for <paramref name="order"/> through to the gateway.</summary>
    private static void SetupNewPaymentFlow(Mocks m, Order order, PaymentGatewayResult gatewayResult)
    {
        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        m.OrderRepo.Setup(r => r.GetByIdWithItemsTrackedAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        m.PaymentRepo.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        Payment? stagedPayment = null;
        m.PaymentRepo.Setup(r => r.Stage(It.IsAny<Payment>()))
            .Callback<Payment>(p => stagedPayment = p);
        m.PaymentRepo.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stagedPayment);

        m.Gateway.Setup(g => g.ProcessAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gatewayResult);
    }

    // --- Idempotency: terminal same-key replay ---

    [TestMethod]
    public async Task CreatePayment_SameKeyAndSucceededPayment_ReturnsExistingWithoutGateway()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var existingPayment = new Payment(orderId, userId, 50m, "payment-key-001");
        existingPayment.MarkSucceeded("fake_ref", DateTimeOffset.UtcNow);

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync("payment-key-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPayment);

        var svc = m.Build();
        var (result, isNew) = await svc.CreatePaymentAsync(userId, MakeRequest(orderId), CancellationToken.None);

        Assert.AreEqual(existingPayment.Id, result.Id);
        Assert.IsFalse(isNew);
        m.Gateway.Verify(g => g.ProcessAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsEmpty(m.StagedOutbox);
    }

    [TestMethod]
    public async Task CreatePayment_SameKeyAndFailedPayment_ReturnsExistingWithoutGateway()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var existingPayment = new Payment(orderId, userId, 50m, "payment-key-001");
        existingPayment.MarkFailed("declined", DateTimeOffset.UtcNow);

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync("payment-key-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPayment);

        var svc = m.Build();
        var (result, isNew) = await svc.CreatePaymentAsync(userId, MakeRequest(orderId), CancellationToken.None);

        Assert.AreEqual(existingPayment.Id, result.Id);
        Assert.IsFalse(isNew);
        m.Gateway.Verify(g => g.ProcessAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsEmpty(m.StagedOutbox);
    }

    // --- Idempotency: same key different Order ---

    [TestMethod]
    public async Task CreatePayment_SameKeyDifferentOrder_ThrowsConflictException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var differentOrderId = Guid.NewGuid();
        var existingPayment = new Payment(differentOrderId, userId, 50m, "payment-key-001");

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync("payment-key-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPayment);

        var svc = m.Build();
        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreatePaymentAsync(userId, MakeRequest(Guid.NewGuid()), CancellationToken.None));

        Assert.AreEqual("Idempotency conflict", ex.ConflictTitle);
    }

    // --- Order ownership ---

    [TestMethod]
    public async Task CreatePayment_OrderNotFound_ThrowsNotFoundException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        m.OrderRepo.Setup(r => r.GetByIdWithItemsTrackedAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var svc = m.Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.CreatePaymentAsync(userId, MakeRequest(orderId), CancellationToken.None));
    }

    [TestMethod]
    public async Task CreatePayment_OrderBelongsToAnotherUser_ThrowsNotFoundException()
    {
        var m = MakeMocks();
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var order = MakePendingOrder(ownerId);

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        m.OrderRepo.Setup(r => r.GetByIdWithItemsTrackedAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var svc = m.Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.CreatePaymentAsync(requesterId, MakeRequest(order.Id), CancellationToken.None));
    }

    // --- Invalid order state ---

    [TestMethod]
    public async Task CreatePayment_ConfirmedOrder_ThrowsConflictException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        order.ConfirmPayment();

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        m.OrderRepo.Setup(r => r.GetByIdWithItemsTrackedAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var svc = m.Build();
        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreatePaymentAsync(userId, MakeRequest(order.Id), CancellationToken.None));

        Assert.AreEqual("Order cannot be paid", ex.ConflictTitle);
    }

    [TestMethod]
    public async Task CreatePayment_DifferentKeyExistingPayment_ThrowsConflictException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        var existingPayment = new Payment(order.Id, userId, 50m, "original-key");

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        m.OrderRepo.Setup(r => r.GetByIdWithItemsTrackedAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        m.PaymentRepo.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPayment);

        var svc = m.Build();
        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreatePaymentAsync(userId, MakeRequest(order.Id, "different-key"), CancellationToken.None));

        Assert.AreEqual("Payment already exists", ex.ConflictTitle);
    }

    // --- Successful payment ---

    [TestMethod]
    public async Task CreatePayment_GatewaySucceeds_MarksPaymentSucceededAndConfirmsOrder()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId, 50m);
        SetupNewPaymentFlow(m, order, new PaymentGatewayResult(true, "fake_ref_123", null));

        var svc = m.Build();
        var (result, isNew) = await svc.CreatePaymentAsync(userId, MakeRequest(order.Id), CancellationToken.None);

        Assert.IsTrue(isNew);
        Assert.AreEqual(PaymentStatus.Succeeded, result.Status);
        Assert.AreEqual("fake_ref_123", result.ProviderReference);
        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
    }

    [TestMethod]
    public async Task CreatePayment_GatewaySucceeds_StagesOnlyOrderStatusChanged()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId, 50m);
        SetupNewPaymentFlow(m, order, new PaymentGatewayResult(true, "fake_ref_123", null));

        var svc = m.Build();
        await svc.CreatePaymentAsync(userId, MakeRequest(order.Id), CancellationToken.None);

        Assert.HasCount(1, m.StagedOutbox);
        Assert.AreEqual(OutboxEventTypes.OrderStatusChanged, m.StagedOutbox[0].Type);
    }

    // --- Declined payment ---

    [TestMethod]
    public async Task CreatePayment_GatewayDeclines_MarksPaymentFailed()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId, 50m);
        SetupNewPaymentFlow(m, order, new PaymentGatewayResult(false, null, "declined"));

        var svc = m.Build();
        var (result, _) = await svc.CreatePaymentAsync(userId, MakeRequest(order.Id), CancellationToken.None);

        Assert.AreEqual(PaymentStatus.Failed, result.Status);
        Assert.AreEqual("declined", result.FailureReason);
        Assert.AreEqual(OrderStatus.PaymentFailed, order.Status);
    }

    [TestMethod]
    public async Task CreatePayment_GatewayDeclines_RestoresStock()
    {
        // Stock restoration is now delegated to the Product service via an OrderPaymentFailed outbox row.
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = new Order(userId, 50m);
        order.AddItem(productId, "Widget", 10m, 3);
        SetupNewPaymentFlow(m, order, new PaymentGatewayResult(false, null, "declined"));

        var svc = m.Build();
        await svc.CreatePaymentAsync(userId, MakeRequest(order.Id), CancellationToken.None);

        var failedRows = m.StagedOutbox.Where(o => o.Type == OutboxEventTypes.OrderPaymentFailed).ToList();
        Assert.HasCount(1, failedRows);

        var evt = JsonSerializer.Deserialize<OrderPaymentFailedEvent>(failedRows[0].Payload, EComJson.Options);
        Assert.IsNotNull(evt);
        Assert.AreEqual(order.Id, evt.OrderId);
        Assert.AreEqual(userId, evt.UserId);
        Assert.HasCount(1, evt.Items);
        Assert.AreEqual(new OrderLineItem(productId, 3), evt.Items[0]);
    }

    [TestMethod]
    public async Task CreatePayment_GatewayDeclines_StagesStatusChangedAndPaymentFailedBeforeSingleSave()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId, 50m);
        SetupNewPaymentFlow(m, order, new PaymentGatewayResult(false, null, "declined"));

        var stagedCountAtFinalSave = -1;
        m.PaymentRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Callback(() => stagedCountAtFinalSave = m.StagedOutbox.Count)
            .Returns(Task.CompletedTask);

        var svc = m.Build();
        await svc.CreatePaymentAsync(userId, MakeRequest(order.Id), CancellationToken.None);

        CollectionAssert.AreEquivalent(
            new[] { OutboxEventTypes.OrderStatusChanged, OutboxEventTypes.OrderPaymentFailed },
            m.StagedOutbox.Select(o => o.Type).ToList());
        Assert.AreEqual(2, stagedCountAtFinalSave);
        // One save for the Pending payment, one for the outcome + outbox rows.
        m.PaymentRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // --- Pending same-key retry ---

    [TestMethod]
    public async Task CreatePayment_SameKeyAndPendingPayment_RetriesGateway()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId, 50m);
        var pending = new Payment(order.Id, userId, 50m, "payment-key-001");

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync("payment-key-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        m.PaymentRepo.Setup(r => r.GetByIdTrackedAsync(pending.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        m.OrderRepo.Setup(r => r.GetByIdWithItemsTrackedAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        m.Gateway.Setup(g => g.ProcessAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentGatewayResult(true, "fake_ref", null));

        var svc = m.Build();
        var (result, isNew) = await svc.CreatePaymentAsync(userId, MakeRequest(order.Id), CancellationToken.None);

        Assert.IsTrue(isNew);
        Assert.AreEqual(pending.Id, result.Id);
        Assert.AreEqual(PaymentStatus.Succeeded, result.Status);
        m.PaymentRepo.Verify(r => r.Stage(It.IsAny<Payment>()), Times.Never);
    }

    // --- Gateway technical failure ---

    [TestMethod]
    public async Task CreatePayment_GatewayThrows_ThrowsGatewayException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);

        m.PaymentRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        m.OrderRepo.Setup(r => r.GetByIdWithItemsTrackedAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        m.PaymentRepo.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        m.Gateway.Setup(g => g.ProcessAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var svc = m.Build();
        await Assert.ThrowsExactlyAsync<GatewayException>(() =>
            svc.CreatePaymentAsync(userId, MakeRequest(order.Id), CancellationToken.None));

        Assert.IsEmpty(m.StagedOutbox);
        Assert.AreEqual(OrderStatus.PendingPayment, order.Status);
    }

    // --- GetPaymentById ---

    [TestMethod]
    public async Task GetPaymentById_Exists_ReturnsPaymentResponse()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var payment = new Payment(Guid.NewGuid(), userId, 50m, "key");

        m.PaymentRepo.Setup(r => r.GetByIdForUserAsync(payment.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var svc = m.Build();
        var result = await svc.GetPaymentByIdAsync(payment.Id, userId);

        Assert.AreEqual(payment.Id, result.Id);
    }

    [TestMethod]
    public async Task GetPaymentById_NotFound_ThrowsNotFoundException()
    {
        var m = MakeMocks();
        m.PaymentRepo.Setup(r => r.GetByIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var svc = m.Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.GetPaymentByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetPaymentById_WrongUser_ThrowsNotFoundException()
    {
        var m = MakeMocks();
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var payment = new Payment(Guid.NewGuid(), ownerId, 50m, "key");

        // GetByIdForUserAsync already filters by userId, so returns null for wrong user
        m.PaymentRepo.Setup(r => r.GetByIdForUserAsync(payment.Id, requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var svc = m.Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.GetPaymentByIdAsync(payment.Id, requesterId));
    }

    // --- GetPaymentByOrderId ---

    [TestMethod]
    public async Task GetPaymentByOrderId_Exists_ReturnsPaymentResponse()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var payment = new Payment(orderId, userId, 50m, "key");

        m.PaymentRepo.Setup(r => r.GetByOrderIdForUserAsync(orderId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var svc = m.Build();
        var result = await svc.GetPaymentByOrderIdAsync(orderId, userId);

        Assert.AreEqual(payment.Id, result.Id);
        Assert.AreEqual(orderId, result.OrderId);
    }

    [TestMethod]
    public async Task GetPaymentByOrderId_NotFound_ThrowsNotFoundException()
    {
        var m = MakeMocks();
        m.PaymentRepo.Setup(r => r.GetByOrderIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var svc = m.Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.GetPaymentByOrderIdAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
