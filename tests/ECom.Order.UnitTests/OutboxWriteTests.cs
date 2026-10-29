namespace ECom.Order.UnitTests;

using System.Text.Json;
using ECom.Order.Api.Clients;
using ECom.Order.Api.DTOs;
using ECom.Order.Api.Entities;
using ECom.Order.Api.Messaging;
using ECom.Order.Api.Repositories;
using ECom.Order.Api.Services;
using ECom.Shared.Messaging;
using ECom.Shared.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Verifies that services stage transactional outbox messages as part of their primary operations.
/// </summary>
[TestClass]
public sealed class OutboxWriteTests
{
    // --- OrderService outbox ---

    private static (
        Mock<IOrderRepository> orderRepo,
        Mock<IOutboxRepository> outboxRepo,
        Mock<ICartClient> cartClient,
        Mock<IProductClient> productClient,
        List<OutboxMessage> staged)
        MakeOrderMocks(Guid userId, params (ProductDto product, int quantity)[] lines)
    {
        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(r => r.Stage(It.IsAny<Order>()));
        orderRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var staged = new List<OutboxMessage>();
        var outboxRepo = new Mock<IOutboxRepository>();
        outboxRepo.Setup(r => r.Stage(It.IsAny<OutboxMessage>())).Callback<OutboxMessage>(staged.Add);

        var cartClient = new Mock<ICartClient>();
        cartClient.Setup(c => c.GetCartAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartSnapshotDto(Guid.NewGuid(), userId,
                lines.Select(l => new CartSnapshotItemDto(Guid.NewGuid(), l.product.Id, l.quantity)).ToList()));

        var productClient = new Mock<IProductClient>();
        productClient.Setup(c => c.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lines.Select(l => l.product).ToList().AsReadOnly());
        productClient.Setup(c => c.DecrementStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (orderRepo, outboxRepo, cartClient, productClient, staged);
    }

    private static OrderService MakeOrderSvc(
        Mock<IOrderRepository> orderRepo,
        Mock<IOutboxRepository> outboxRepo,
        Mock<ICartClient> cartClient,
        Mock<IProductClient> productClient) =>
        new(orderRepo.Object, outboxRepo.Object, cartClient.Object, productClient.Object,
            NullLogger<OrderService>.Instance);

    private static ProductDto MakeProduct(decimal price = 10m, int stock = 5) =>
        new(Guid.NewGuid(), "Widget", "Desc", price, "Cat", stock, null);

    [TestMethod]
    public async Task CreateOrder_StagesOutboxMessage_WithOrderStatusChangedType()
    {
        var userId = Guid.NewGuid();
        var (orderRepo, outboxRepo, cartClient, productClient, staged) =
            MakeOrderMocks(userId, (MakeProduct(), 1));

        var svc = MakeOrderSvc(orderRepo, outboxRepo, cartClient, productClient);
        await svc.CreateOrderAsync(userId);

        Assert.HasCount(1, staged.Where(m => m.Type == OutboxEventTypes.OrderStatusChanged));
    }

    [TestMethod]
    public async Task CreateOrder_StagesOrderStatusChangedAndOrderCreated()
    {
        var userId = Guid.NewGuid();
        var (orderRepo, outboxRepo, cartClient, productClient, staged) =
            MakeOrderMocks(userId, (MakeProduct(), 1));

        var svc = MakeOrderSvc(orderRepo, outboxRepo, cartClient, productClient);
        await svc.CreateOrderAsync(userId);

        CollectionAssert.AreEquivalent(
            new[] { OutboxEventTypes.OrderStatusChanged, OutboxEventTypes.OrderCreated },
            staged.Select(m => m.Type).ToList());
    }

    [TestMethod]
    public async Task CreateOrder_OutboxPayload_ContainsPendingPaymentStatus()
    {
        var userId = Guid.NewGuid();
        var (orderRepo, outboxRepo, cartClient, productClient, staged) =
            MakeOrderMocks(userId, (MakeProduct(), 2));

        var svc = MakeOrderSvc(orderRepo, outboxRepo, cartClient, productClient);
        var order = await svc.CreateOrderAsync(userId);

        var msg = staged.Single(m => m.Type == OutboxEventTypes.OrderStatusChanged);
        var notification = JsonSerializer.Deserialize<OrderStatusChangedNotification>(msg.Payload);
        Assert.IsNotNull(notification);
        Assert.AreEqual(order.Id, notification.OrderId);
        Assert.AreEqual(userId, notification.UserId);
        Assert.AreEqual(OrderStatus.PendingPayment.ToString(), notification.Status);
    }

    [TestMethod]
    public async Task CreateOrder_OrderCreatedPayload_ContainsOrderUserAndLines()
    {
        var userId = Guid.NewGuid();
        var productA = MakeProduct(10m, 5);
        var productB = MakeProduct(5m, 10);
        var (orderRepo, outboxRepo, cartClient, productClient, staged) =
            MakeOrderMocks(userId, (productA, 2), (productB, 3));

        var svc = MakeOrderSvc(orderRepo, outboxRepo, cartClient, productClient);
        var order = await svc.CreateOrderAsync(userId);

        var msg = staged.Single(m => m.Type == OutboxEventTypes.OrderCreated);
        var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(msg.Payload, EComJson.Options);
        Assert.IsNotNull(evt);
        Assert.AreEqual(order.Id, evt.OrderId);
        Assert.AreEqual(userId, evt.UserId);
        CollectionAssert.AreEquivalent(
            new[] { new OrderLineItem(productA.Id, 2), new OrderLineItem(productB.Id, 3) },
            evt.Items.ToList());
        Assert.AreEqual(msg.OccurredAt, evt.OccurredAt);
    }

    [TestMethod]
    public async Task CreateOrder_OrderCreatedPayload_IsCamelCase()
    {
        var userId = Guid.NewGuid();
        var (orderRepo, outboxRepo, cartClient, productClient, staged) =
            MakeOrderMocks(userId, (MakeProduct(), 1));

        var svc = MakeOrderSvc(orderRepo, outboxRepo, cartClient, productClient);
        await svc.CreateOrderAsync(userId);

        var msg = staged.Single(m => m.Type == OutboxEventTypes.OrderCreated);
        using var doc = JsonDocument.Parse(msg.Payload);
        Assert.IsTrue(doc.RootElement.TryGetProperty("orderId", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("userId", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("items", out var items));
        Assert.IsTrue(items[0].TryGetProperty("productId", out _));
        Assert.IsTrue(items[0].TryGetProperty("quantity", out _));
    }

    [TestMethod]
    public async Task CreateOrder_OutboxStagedBeforeSave()
    {
        // Verify Stage is called before SaveAsync by checking call order
        var userId = Guid.NewGuid();
        var (orderRepo, outboxRepo, cartClient, productClient, staged) =
            MakeOrderMocks(userId, (MakeProduct(), 1));

        var stagedCountAtSave = -1;
        orderRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Callback(() => stagedCountAtSave = staged.Count)
            .Returns(Task.CompletedTask);

        var svc = MakeOrderSvc(orderRepo, outboxRepo, cartClient, productClient);
        await svc.CreateOrderAsync(userId);

        Assert.AreEqual(2, stagedCountAtSave, "Both outbox messages must be staged before SaveAsync is called.");
    }

    // --- PaymentService outbox ---

    private static (
        Mock<IPaymentRepository> paymentRepo,
        Mock<IOrderRepository> orderRepo,
        Mock<IPaymentGateway> gateway,
        Mock<IOutboxRepository> outboxRepo,
        List<OutboxMessage> staged)
        MakePaymentMocks()
    {
        var paymentRepo = new Mock<IPaymentRepository>();
        paymentRepo.Setup(r => r.Stage(It.IsAny<Payment>()));
        paymentRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var orderRepo = new Mock<IOrderRepository>();
        var gateway = new Mock<IPaymentGateway>();

        var staged = new List<OutboxMessage>();
        var outboxRepo = new Mock<IOutboxRepository>();
        outboxRepo.Setup(r => r.Stage(It.IsAny<OutboxMessage>())).Callback<OutboxMessage>(staged.Add);

        return (paymentRepo, orderRepo, gateway, outboxRepo, staged);
    }

    private static PaymentService MakePaymentSvc(
        Mock<IPaymentRepository> paymentRepo,
        Mock<IOrderRepository> orderRepo,
        Mock<IPaymentGateway> gateway,
        Mock<IOutboxRepository> outboxRepo) =>
        new(paymentRepo.Object, orderRepo.Object, gateway.Object,
            outboxRepo.Object, NullLogger<PaymentService>.Instance);

    private static Order MakePendingOrder(Guid userId)
    {
        var order = new Order(userId, 50m);
        order.AddItem(Guid.NewGuid(), "Widget", 50m, 1);
        return order;
    }

    private static void SetupPaymentFlow(
        Mock<IPaymentRepository> paymentRepo,
        Mock<IOrderRepository> orderRepo,
        Mock<IPaymentGateway> gateway,
        Order order,
        PaymentGatewayResult result)
    {
        paymentRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        orderRepo.Setup(r => r.GetByIdWithItemsTrackedAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        paymentRepo.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        Payment? staged = null;
        paymentRepo.Setup(r => r.Stage(It.IsAny<Payment>()))
            .Callback<Payment>(p => staged = p);
        paymentRepo.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => staged);

        gateway.Setup(g => g.ProcessAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    [TestMethod]
    public async Task CreatePayment_Success_StagesOutboxWithConfirmedStatus()
    {
        var (paymentRepo, orderRepo, gateway, outboxRepo, staged) = MakePaymentMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        SetupPaymentFlow(paymentRepo, orderRepo, gateway, order, new PaymentGatewayResult(true, "fake_ref_123", null));

        var svc = MakePaymentSvc(paymentRepo, orderRepo, gateway, outboxRepo);
        await svc.CreatePaymentAsync(userId, new CreatePaymentRequest { OrderId = order.Id, IdempotencyKey = "key-001" });

        Assert.HasCount(1, staged);
        var outboxMsg = staged[0];
        Assert.AreEqual(OutboxEventTypes.OrderStatusChanged, outboxMsg.Type);
        var notification = JsonSerializer.Deserialize<OrderStatusChangedNotification>(outboxMsg.Payload);
        Assert.AreEqual(OrderStatus.Confirmed.ToString(), notification!.Status);
        Assert.AreEqual(order.Id, notification.OrderId);
        Assert.AreEqual(userId, notification.UserId);
    }

    [TestMethod]
    public async Task CreatePayment_Decline_StagesOutboxWithPaymentFailedStatus()
    {
        var (paymentRepo, orderRepo, gateway, outboxRepo, staged) = MakePaymentMocks();
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        SetupPaymentFlow(paymentRepo, orderRepo, gateway, order, new PaymentGatewayResult(false, null, "declined"));

        var svc = MakePaymentSvc(paymentRepo, orderRepo, gateway, outboxRepo);
        await svc.CreatePaymentAsync(userId, new CreatePaymentRequest { OrderId = order.Id, IdempotencyKey = "key-001" });

        var outboxMsg = staged.Single(m => m.Type == OutboxEventTypes.OrderStatusChanged);
        var notification = JsonSerializer.Deserialize<OrderStatusChangedNotification>(outboxMsg.Payload);
        Assert.AreEqual(OrderStatus.PaymentFailed.ToString(), notification!.Status);
    }

    [TestMethod]
    public async Task CreatePayment_Decline_StagesOrderPaymentFailedEventWithItems()
    {
        var (paymentRepo, orderRepo, gateway, outboxRepo, staged) = MakePaymentMocks();
        var userId = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var order = new Order(userId, 50m);
        order.AddItem(productA, "A", 10m, 2);
        order.AddItem(productB, "B", 10m, 3);
        SetupPaymentFlow(paymentRepo, orderRepo, gateway, order, new PaymentGatewayResult(false, null, "declined"));

        var svc = MakePaymentSvc(paymentRepo, orderRepo, gateway, outboxRepo);
        await svc.CreatePaymentAsync(userId, new CreatePaymentRequest { OrderId = order.Id, IdempotencyKey = "key-001" });

        Assert.HasCount(2, staged);
        var msg = staged.Single(m => m.Type == OutboxEventTypes.OrderPaymentFailed);
        var evt = JsonSerializer.Deserialize<OrderPaymentFailedEvent>(msg.Payload, EComJson.Options);
        Assert.IsNotNull(evt);
        Assert.AreEqual(order.Id, evt.OrderId);
        Assert.AreEqual(userId, evt.UserId);
        CollectionAssert.AreEquivalent(
            new[] { new OrderLineItem(productA, 2), new OrderLineItem(productB, 3) },
            evt.Items.ToList());

        using var doc = JsonDocument.Parse(msg.Payload);
        Assert.IsTrue(doc.RootElement.TryGetProperty("orderId", out _), "Payload must be camelCase (EComJson).");
    }
}
