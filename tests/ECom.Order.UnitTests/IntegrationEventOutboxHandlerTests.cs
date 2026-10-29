namespace ECom.Order.UnitTests;

using ECom.Order.Api.Messaging;
using ECom.Shared.Messaging;
using ECom.Shared.Outbox;
using Moq;

[TestClass]
public sealed class IntegrationEventOutboxHandlerTests
{
    [TestMethod]
    public void Type_ReturnsConfiguredType()
    {
        var handler = new IntegrationEventOutboxHandler(
            OutboxEventTypes.OrderPaymentFailed, "order-payment-failed", new Mock<IMessageBus>().Object);

        Assert.AreEqual(OutboxEventTypes.OrderPaymentFailed, handler.Type);
    }

    [TestMethod]
    public async Task HandleAsync_PublishesPayloadToConfiguredEntity_WithSubjectAndMessageId()
    {
        var busMock = new Mock<IMessageBus>();
        busMock.Setup(b => b.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new IntegrationEventOutboxHandler(OutboxEventTypes.OrderCreated, "order-created", busMock.Object);
        var payload = "{\"orderId\":\"6f1c1a4e-0000-0000-0000-000000000001\"}";
        var message = new OutboxMessage(OutboxEventTypes.OrderCreated, payload, DateTimeOffset.UtcNow);
        using var cts = new CancellationTokenSource();

        await handler.HandleAsync(message, cts.Token);

        busMock.Verify(b => b.PublishAsync(
            "order-created", OutboxEventTypes.OrderCreated, payload, message.Id.ToString(), cts.Token), Times.Once);
        busMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task HandleAsync_BusThrows_ExceptionPropagates()
    {
        var busMock = new Mock<IMessageBus>();
        busMock.Setup(b => b.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus down"));
        var handler = new IntegrationEventOutboxHandler(OutboxEventTypes.OrderCreated, "order-created", busMock.Object);
        var message = new OutboxMessage(OutboxEventTypes.OrderCreated, "{}", DateTimeOffset.UtcNow);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => handler.HandleAsync(message));

        Assert.AreEqual("bus down", ex.Message);
    }
}
