namespace ECom.Product.UnitTests;

using ECom.Product.Api.DTOs;
using ECom.Product.Api.Messaging;
using ECom.Product.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestClass]
public sealed class OrderPaymentFailedHandlerTests
{
    private static OrderPaymentFailedHandler CreateHandler(IStockService stockService) =>
        new(stockService, NullLogger<OrderPaymentFailedHandler>.Instance);

    private static OrderPaymentFailedEvent MakeEvent(params OrderPaymentFailedItem[] items) =>
        new(Guid.NewGuid(), Guid.NewGuid(), items, DateTimeOffset.UtcNow);

    [TestMethod]
    public async Task HandleAsync_MapsItemsToStockLinesAndCallsRestore()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var evt = MakeEvent(new OrderPaymentFailedItem(p1, 2), new OrderPaymentFailedItem(p2, 5));

        IReadOnlyList<StockLine>? captured = null;
        var stockMock = new Mock<IStockService>();
        stockMock.Setup(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLine>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<StockLine>, CancellationToken>((_, lines, _) => captured = lines)
            .Returns(Task.CompletedTask);

        await CreateHandler(stockMock.Object).HandleAsync(evt, "msg-1");

        stockMock.Verify(s => s.RestoreAsync(evt.OrderId, It.IsAny<IReadOnlyList<StockLine>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsNotNull(captured);
        CollectionAssert.AreEqual(
            new[] { new StockLine(p1, 2), new StockLine(p2, 5) },
            captured!.ToList());
    }

    [TestMethod]
    public async Task HandleAsync_PassesCancellationTokenToRestore()
    {
        using var cts = new CancellationTokenSource();
        var evt = MakeEvent(new OrderPaymentFailedItem(Guid.NewGuid(), 1));
        var stockMock = new Mock<IStockService>();
        stockMock.Setup(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLine>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateHandler(stockMock.Object).HandleAsync(evt, "msg-1", cts.Token);

        stockMock.Verify(s => s.RestoreAsync(evt.OrderId, It.IsAny<IReadOnlyList<StockLine>>(), cts.Token), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_DuplicateProductItems_ArePassedThroughForServiceToAggregate()
    {
        var productId = Guid.NewGuid();
        var evt = MakeEvent(new OrderPaymentFailedItem(productId, 1), new OrderPaymentFailedItem(productId, 2));

        IReadOnlyList<StockLine>? captured = null;
        var stockMock = new Mock<IStockService>();
        stockMock.Setup(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLine>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<StockLine>, CancellationToken>((_, lines, _) => captured = lines)
            .Returns(Task.CompletedTask);

        await CreateHandler(stockMock.Object).HandleAsync(evt, "msg-1");

        Assert.IsNotNull(captured);
        Assert.HasCount(2, captured!);
    }

    [TestMethod]
    public async Task HandleAsync_RestoreFailure_Propagates()
    {
        var evt = MakeEvent(new OrderPaymentFailedItem(Guid.NewGuid(), 1));
        var stockMock = new Mock<IStockService>();
        stockMock.Setup(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<StockLine>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        // Exceptions must surface so Service Bus retries / dead-letters the message.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            CreateHandler(stockMock.Object).HandleAsync(evt, "msg-1"));
    }
}
