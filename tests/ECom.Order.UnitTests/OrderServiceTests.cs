namespace ECom.Order.UnitTests;

using ECom.Order.Api.Clients;
using ECom.Order.Api.Entities;
using ECom.Order.Api.Repositories;
using ECom.Order.Api.Services;
using ECom.Shared.Exceptions;
using ECom.Shared.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestClass]
public sealed class OrderServiceTests
{
    private static ProductDto MakeProduct(string name = "Widget", decimal price = 10.00m, int stock = 10) =>
        new(Guid.NewGuid(), name, "Description", price, "Category", stock, null);

    private static CartSnapshotDto MakeCart(Guid userId, params (Guid productId, int quantity)[] lines) =>
        new(Guid.NewGuid(), userId,
            lines.Select(l => new CartSnapshotItemDto(Guid.NewGuid(), l.productId, l.quantity)).ToList());

    private sealed record Mocks(
        Mock<IOrderRepository> OrderRepo,
        Mock<IOutboxRepository> OutboxRepo,
        Mock<ICartClient> CartClient,
        Mock<IProductClient> ProductClient)
    {
        public OrderService Build() =>
            new(OrderRepo.Object, OutboxRepo.Object, CartClient.Object, ProductClient.Object,
                NullLogger<OrderService>.Instance);

        public void SetupCart(CartSnapshotDto cart) =>
            CartClient.Setup(c => c.GetCartAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        public void SetupProducts(params ProductDto[] products) =>
            ProductClient.Setup(c => c.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(products.ToList().AsReadOnly());
    }

    private static Mocks MakeMocks()
    {
        var orderRepo = new Mock<IOrderRepository>();
        orderRepo.Setup(r => r.Stage(It.IsAny<Order>()));
        orderRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outboxRepo = new Mock<IOutboxRepository>();
        outboxRepo.Setup(r => r.Stage(It.IsAny<OutboxMessage>()));

        var cartClient = new Mock<ICartClient>();
        cartClient.Setup(c => c.GetCartAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartSnapshotDto(Guid.Empty, Guid.Empty, new List<CartSnapshotItemDto>()));

        var productClient = new Mock<IProductClient>();
        productClient.Setup(c => c.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>().AsReadOnly());
        productClient.Setup(c => c.DecrementStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        productClient.Setup(c => c.RestoreStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new(orderRepo, outboxRepo, cartClient, productClient);
    }

    // --- Empty / missing cart ---

    [TestMethod]
    public async Task CreateOrder_NullCart_ThrowsConflictException()
    {
        // The Cart service represents "no cart" as an empty snapshot (the MakeMocks default).
        var m = MakeMocks();
        var svc = m.Build();

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreateOrderAsync(Guid.NewGuid()));

        Assert.AreEqual("Cart is empty", ex.ConflictTitle);
    }

    [TestMethod]
    public async Task CreateOrder_EmptyCart_ThrowsConflictException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        m.SetupCart(MakeCart(userId));

        var svc = m.Build();

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreateOrderAsync(userId));

        Assert.AreEqual("Cart is empty", ex.ConflictTitle);
        m.ProductClient.Verify(c => c.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Missing product ---

    [TestMethod]
    public async Task CreateOrder_MissingProduct_ThrowsConflictException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        m.SetupCart(MakeCart(userId, (Guid.NewGuid(), 1)));

        var svc = m.Build();

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreateOrderAsync(userId));

        Assert.AreEqual("Product unavailable", ex.ConflictTitle);
        m.ProductClient.Verify(c => c.DecrementStockAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Insufficient stock ---

    [TestMethod]
    public async Task CreateOrder_InsufficientStock_ThrowsConflictException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 2);
        m.SetupCart(MakeCart(userId, (product.Id, 5)));
        m.SetupProducts(product);

        var svc = m.Build();

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.CreateOrderAsync(userId));

        Assert.AreEqual("Insufficient stock", ex.ConflictTitle);
    }

    [TestMethod]
    public async Task CreateOrder_InsufficientStock_DoesNotStageOrder()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 1);
        m.SetupCart(MakeCart(userId, (product.Id, 3)));
        m.SetupProducts(product);

        var svc = m.Build();

        await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.CreateOrderAsync(userId));

        m.OrderRepo.Verify(r => r.Stage(It.IsAny<Order>()), Times.Never);
        m.OrderRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.OutboxRepo.Verify(r => r.Stage(It.IsAny<OutboxMessage>()), Times.Never);
        m.ProductClient.Verify(c => c.DecrementStockAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task CreateOrder_DecrementStockConflict_PropagatesAndDoesNotStageOrder()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 5);
        m.SetupCart(MakeCart(userId, (product.Id, 2)));
        m.SetupProducts(product);
        m.ProductClient.Setup(c => c.DecrementStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException(
                "One or more products do not have enough stock to fulfill the order.", "Insufficient stock"));

        var svc = m.Build();

        var ex = await Assert.ThrowsExactlyAsync<ConflictException>(() => svc.CreateOrderAsync(userId));

        Assert.AreEqual("Insufficient stock", ex.ConflictTitle);
        m.OrderRepo.Verify(r => r.Stage(It.IsAny<Order>()), Times.Never);
        m.OrderRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.OutboxRepo.Verify(r => r.Stage(It.IsAny<OutboxMessage>()), Times.Never);
        m.ProductClient.Verify(c => c.RestoreStockAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Successful order creation ---

    [TestMethod]
    public async Task CreateOrder_ValidCart_ReturnsOrderResponse()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(price: 10.50m, stock: 5);
        m.SetupCart(MakeCart(userId, (product.Id, 2)));
        m.SetupProducts(product);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.IsNotNull(result);
        Assert.AreNotEqual(Guid.Empty, result.Id);
        Assert.AreEqual(OrderStatus.PendingPayment, result.Status);
        Assert.AreEqual(21.00m, result.TotalAmount);
        Assert.HasCount(1, result.Items);
    }

    [TestMethod]
    public async Task CreateOrder_ValidCart_OneOrderItemPerCartLine()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var productA = MakeProduct("Product A", 10.50m, 5);
        var productB = MakeProduct("Product B", 5.25m, 10);
        m.SetupCart(MakeCart(userId, (productA.Id, 2), (productB.Id, 3)));
        m.SetupProducts(productA, productB);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.HasCount(2, result.Items);
    }

    [TestMethod]
    public async Task CreateOrder_WorkedExample_TotalAmount36_75()
    {
        // Product A price 10.50, quantity 2 → 21.00
        // Product B price 5.25,  quantity 3 → 15.75
        // Total = 36.75
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var productA = MakeProduct("Product A", 10.50m, 5);
        var productB = MakeProduct("Product B", 5.25m, 10);
        m.SetupCart(MakeCart(userId, (productA.Id, 2), (productB.Id, 3)));
        m.SetupProducts(productA, productB);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.AreEqual(36.75m, result.TotalAmount);
    }

    [TestMethod]
    public async Task CreateOrder_ProductNameSnapshot_UsesCurrentProductName()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct("Current Name", stock: 5);
        m.SetupCart(MakeCart(userId, (product.Id, 1)));
        m.SetupProducts(product);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.AreEqual("Current Name", result.Items[0].ProductName);
    }

    [TestMethod]
    public async Task CreateOrder_UnitPriceSnapshot_UsesCurrentProductPrice()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(price: 19.99m, stock: 5);
        m.SetupCart(MakeCart(userId, (product.Id, 1)));
        m.SetupProducts(product);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.AreEqual(19.99m, result.Items[0].UnitPrice);
    }

    [TestMethod]
    public async Task CreateOrder_QuantitySnapshot_UsesCartItemQuantity()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);
        m.SetupCart(MakeCart(userId, (product.Id, 4)));
        m.SetupProducts(product);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.AreEqual(4, result.Items[0].Quantity);
    }

    [TestMethod]
    public async Task CreateOrder_RequestsDistinctProductIdsFromProductService()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var productA = MakeProduct("A", 10.00m, 5);
        var productB = MakeProduct("B", 5.00m, 10);
        m.SetupCart(MakeCart(userId, (productA.Id, 2), (productB.Id, 3)));

        List<Guid>? requested = null;
        m.ProductClient.Setup(c => c.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, CancellationToken>((ids, _) => requested = ids.ToList())
            .ReturnsAsync(new List<ProductDto> { productA, productB }.AsReadOnly());

        var svc = m.Build();
        await svc.CreateOrderAsync(userId);

        Assert.IsNotNull(requested);
        CollectionAssert.AreEquivalent(new[] { productA.Id, productB.Id }, requested);
    }

    [TestMethod]
    public async Task CreateOrder_DecrementStockCorrectly()
    {
        // Stock is now decremented by the Product service; verify the request sent to it.
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var productA = MakeProduct("A", 10.00m, 5);
        var productB = MakeProduct("B", 5.00m, 10);
        m.SetupCart(MakeCart(userId, (productA.Id, 2), (productB.Id, 3)));
        m.SetupProducts(productA, productB);

        Guid? decrementOrderId = null;
        IReadOnlyList<StockLineDto>? decrementLines = null;
        m.ProductClient.Setup(c => c.DecrementStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<StockLineDto>, CancellationToken>((id, lines, _) =>
            {
                decrementOrderId = id;
                decrementLines = lines;
            })
            .Returns(Task.CompletedTask);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        m.ProductClient.Verify(c => c.DecrementStockAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(result.Id, decrementOrderId);
        Assert.IsNotNull(decrementLines);
        CollectionAssert.AreEquivalent(
            new[] { new StockLineDto(productA.Id, 2), new StockLineDto(productB.Id, 3) },
            decrementLines.ToList());
    }

    [TestMethod]
    public async Task CreateOrder_DecrementStock_CalledBeforeSave()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);
        m.SetupCart(MakeCart(userId, (product.Id, 1)));
        m.SetupProducts(product);

        var calls = new List<string>();
        m.ProductClient.Setup(c => c.DecrementStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("decrement"))
            .Returns(Task.CompletedTask);
        m.OrderRepo.Setup(r => r.Stage(It.IsAny<Order>())).Callback(() => calls.Add("stage"));
        m.OrderRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save"))
            .Returns(Task.CompletedTask);

        var svc = m.Build();
        await svc.CreateOrderAsync(userId);

        CollectionAssert.AreEqual(new[] { "decrement", "stage", "save" }, calls);
    }

    [TestMethod]
    public async Task CreateOrder_CartNotClearedByOrderService()
    {
        // Replaces monolith CreateOrder_CartClearedAfterSuccess: the Cart service clears the cart on OrderCreated.
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);
        m.SetupCart(MakeCart(userId, (product.Id, 2)));
        m.SetupProducts(product);

        var svc = m.Build();
        await svc.CreateOrderAsync(userId);

        m.CartClient.Verify(c => c.GetCartAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.CartClient.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task CreateOrder_StagesOrderAndSavesOnce()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);
        m.SetupCart(MakeCart(userId, (product.Id, 1)));
        m.SetupProducts(product);

        var svc = m.Build();
        await svc.CreateOrderAsync(userId);

        m.OrderRepo.Verify(r => r.Stage(It.IsAny<Order>()), Times.Once);
        m.OrderRepo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        m.ProductClient.Verify(c => c.RestoreStockAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task CreateOrder_CancellationTokenForwardedToClientsAndSave()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);
        m.SetupCart(MakeCart(userId, (product.Id, 1)));
        m.SetupProducts(product);
        using var cts = new CancellationTokenSource();

        var svc = m.Build();
        await svc.CreateOrderAsync(userId, cts.Token);

        m.CartClient.Verify(c => c.GetCartAsync(cts.Token), Times.Once);
        m.ProductClient.Verify(c => c.GetProductsAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
        m.ProductClient.Verify(c => c.DecrementStockAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), cts.Token), Times.Once);
        m.OrderRepo.Verify(r => r.SaveAsync(cts.Token), Times.Once);
    }

    // --- Compensation on save failure ---

    [TestMethod]
    public async Task CreateOrder_SaveFails_RestoresStockAndRethrows()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var productA = MakeProduct("A", 10.00m, 5);
        var productB = MakeProduct("B", 5.00m, 10);
        m.SetupCart(MakeCart(userId, (productA.Id, 2), (productB.Id, 3)));
        m.SetupProducts(productA, productB);

        Order? stagedOrder = null;
        m.OrderRepo.Setup(r => r.Stage(It.IsAny<Order>())).Callback<Order>(o => stagedOrder = o);
        var saveError = new InvalidOperationException("db down");
        m.OrderRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).ThrowsAsync(saveError);

        IReadOnlyList<StockLineDto>? decrementLines = null;
        m.ProductClient.Setup(c => c.DecrementStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<StockLineDto>, CancellationToken>((_, lines, _) => decrementLines = lines)
            .Returns(Task.CompletedTask);

        Guid? restoreOrderId = null;
        IReadOnlyList<StockLineDto>? restoreLines = null;
        CancellationToken? restoreToken = null;
        m.ProductClient.Setup(c => c.RestoreStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<StockLineDto>, CancellationToken>((id, lines, ct) =>
            {
                restoreOrderId = id;
                restoreLines = lines;
                restoreToken = ct;
            })
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource();
        var svc = m.Build();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            svc.CreateOrderAsync(userId, cts.Token));

        Assert.AreSame(saveError, ex);
        Assert.IsNotNull(stagedOrder);
        m.ProductClient.Verify(c => c.RestoreStockAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(stagedOrder.Id, restoreOrderId);
        Assert.AreEqual(CancellationToken.None, restoreToken);
        Assert.IsNotNull(restoreLines);
        CollectionAssert.AreEquivalent(
            new[] { new StockLineDto(productA.Id, 2), new StockLineDto(productB.Id, 3) },
            restoreLines.ToList());
        CollectionAssert.AreEquivalent(decrementLines!.ToList(), restoreLines.ToList());
    }

    [TestMethod]
    public async Task CreateOrder_SaveFailsAndCompensationFails_RethrowsOriginalException()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 10);
        m.SetupCart(MakeCart(userId, (product.Id, 1)));
        m.SetupProducts(product);

        var saveError = new InvalidOperationException("db down");
        m.OrderRepo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).ThrowsAsync(saveError);
        m.ProductClient.Setup(c => c.RestoreStockAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DownstreamServiceException("Product service", "Product service is unreachable."));

        var svc = m.Build();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => svc.CreateOrderAsync(userId));

        Assert.AreSame(saveError, ex);
        m.ProductClient.Verify(c => c.RestoreStockAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLineDto>>(), CancellationToken.None), Times.Once);
    }

    [TestMethod]
    public async Task CreateOrder_LineTotal_IsUnitPriceTimesQuantity()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(price: 10.50m, stock: 5);
        m.SetupCart(MakeCart(userId, (product.Id, 3)));
        m.SetupProducts(product);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.AreEqual(31.50m, result.Items[0].LineTotal);
    }

    [TestMethod]
    public async Task CreateOrder_OrderStatus_IsPendingPayment()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 5);
        m.SetupCart(MakeCart(userId, (product.Id, 1)));
        m.SetupProducts(product);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.AreEqual(OrderStatus.PendingPayment, result.Status);
    }

    [TestMethod]
    public async Task CreateOrder_OrderDate_IsInUtc()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var product = MakeProduct(stock: 5);
        m.SetupCart(MakeCart(userId, (product.Id, 1)));
        m.SetupProducts(product);

        var svc = m.Build();
        var result = await svc.CreateOrderAsync(userId);

        Assert.AreEqual(TimeSpan.Zero, result.OrderDate.Offset);
    }

    // --- GetMyOrders ---

    [TestMethod]
    public async Task GetMyOrders_NoOrders_ReturnsEmptyList()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();

        m.OrderRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>().AsReadOnly());

        var svc = m.Build();
        var result = await svc.GetMyOrdersAsync(userId);

        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetMyOrders_ReturnsOnlyRequestedUsersOrders()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = new Order(userId, 10m);

        m.OrderRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order }.AsReadOnly());

        var svc = m.Build();
        var result = await svc.GetMyOrdersAsync(userId);

        Assert.HasCount(1, result);
        Assert.AreEqual(order.Id, result[0].Id);
    }

    [TestMethod]
    public async Task GetMyOrders_CancellationTokenForwarded()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        m.OrderRepo.Setup(r => r.GetByUserIdWithItemsAsync(userId, cts.Token))
            .ReturnsAsync(new List<Order>().AsReadOnly());

        var svc = m.Build();
        await svc.GetMyOrdersAsync(userId, cts.Token);

        m.OrderRepo.Verify(r => r.GetByUserIdWithItemsAsync(userId, cts.Token), Times.Once);
    }

    // --- GetOrderById ---

    [TestMethod]
    public async Task GetOrderById_ExistsAndOwned_ReturnsOrderResponse()
    {
        var m = MakeMocks();
        var userId = Guid.NewGuid();
        var order = new Order(userId, 50m);

        m.OrderRepo.Setup(r => r.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var svc = m.Build();
        var result = await svc.GetOrderByIdAsync(order.Id, userId);

        Assert.IsNotNull(result);
        Assert.AreEqual(order.Id, result.Id);
        Assert.AreEqual(50m, result.TotalAmount);
    }

    [TestMethod]
    public async Task GetOrderById_NotFound_ThrowsNotFoundException()
    {
        var m = MakeMocks();
        m.OrderRepo.Setup(r => r.GetByIdWithItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var svc = m.Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.GetOrderByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetOrderById_WrongUser_ThrowsNotFoundException()
    {
        var m = MakeMocks();
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var order = new Order(ownerId, 50m);

        m.OrderRepo.Setup(r => r.GetByIdWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var svc = m.Build();
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.GetOrderByIdAsync(order.Id, requesterId));
    }

    // --- GetAllOrders ---

    [TestMethod]
    public async Task GetAllOrders_NoOrders_ReturnsEmptyList()
    {
        var m = MakeMocks();
        m.OrderRepo.Setup(r => r.GetAllWithItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>().AsReadOnly());

        var svc = m.Build();
        var result = await svc.GetAllOrdersAsync();

        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetAllOrders_MultipleUsers_ReturnsAllOrders()
    {
        var m = MakeMocks();
        var order1 = new Order(Guid.NewGuid(), 10m);
        var order2 = new Order(Guid.NewGuid(), 20m);

        m.OrderRepo.Setup(r => r.GetAllWithItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order1, order2 }.AsReadOnly());

        var svc = m.Build();
        var result = await svc.GetAllOrdersAsync();

        Assert.HasCount(2, result);
    }
}
