namespace ECom.Order.UnitTests;

using System.Text.Json;
using ECom.Order.Api.DTOs;
using ECom.Order.Api.Messaging;
using ECom.Order.Api.Services;
using ECom.Shared.Messaging;
using ECom.Shared.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// Ported from the monolith's NotificationOutboxProcessorTests: the generic <see cref="OutboxProcessor"/>
/// is exercised with an <see cref="OrderStatusChangedOutboxHandler"/> wrapping a mocked <see cref="IOrderNotifier"/>.
/// </summary>
[TestClass]
public sealed class OutboxProcessorTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);

    private static readonly OutboxOptions DefaultOptions = new();

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static OutboxProcessor CreateProcessor(
        IOutboxRepository outboxRepo,
        IOrderNotifier notifier,
        OutboxOptions? options = null,
        DateTimeOffset? now = null,
        params IOutboxMessageHandler[] extraHandlers) =>
        new(outboxRepo,
            new IOutboxMessageHandler[] { new OrderStatusChangedOutboxHandler(notifier) }.Concat(extraHandlers),
            Options.Create(options ?? DefaultOptions),
            new FixedTimeProvider(now ?? FixedNow),
            NullLogger<OutboxProcessor>.Instance);

    private static string MakePayload(Guid? orderId = null, Guid? userId = null, string status = "Confirmed") =>
        JsonSerializer.Serialize(new OrderStatusChangedNotification(
            orderId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            status,
            FixedNow));

    private static OutboxMessage MakeMessage(string? type = null, string? payload = null) =>
        new(type ?? OutboxEventTypes.OrderStatusChanged, payload ?? MakePayload(), FixedNow);

    private static Mock<IOutboxRepository> MakeOutbox(params OutboxMessage[] messages)
    {
        var outboxMock = new Mock<IOutboxRepository>();
        outboxMock.Setup(r => r.GetPendingBatchAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages.ToList());
        outboxMock.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return outboxMock;
    }

    private static Mock<IOrderNotifier> MakeNotifier(Exception? throws = null)
    {
        var notifierMock = new Mock<IOrderNotifier>();
        var setup = notifierMock.Setup(n => n.NotifyOrderStatusChangedAsync(
            It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()));
        if (throws is null)
            setup.Returns(Task.CompletedTask);
        else
            setup.ThrowsAsync(throws);
        return notifierMock;
    }

    // --- Empty batch ---

    [TestMethod]
    public async Task ProcessBatch_EmptyBatch_NoNotifierCallsAndNoSave()
    {
        var outboxMock = MakeOutbox();
        var notifierMock = new Mock<IOrderNotifier>();
        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);

        await processor.ProcessBatchAsync();

        notifierMock.Verify(n => n.NotifyOrderStatusChangedAsync(
            It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        outboxMock.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Successful dispatch ---

    [TestMethod]
    public async Task ProcessBatch_SuccessfulDispatch_MarkProcessedCalled()
    {
        var msg = MakeMessage();
        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier();

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.IsNotNull(msg.ProcessedAt);
        Assert.AreEqual(FixedNow, msg.ProcessedAt!.Value);
        Assert.AreEqual(0, msg.AttemptCount); // success does not increment attempt count
        Assert.IsNull(msg.LastError);
    }

    [TestMethod]
    public async Task ProcessBatch_SuccessfulDispatch_NotifierCalledWithCorrectPayload()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var msg = MakeMessage(payload: MakePayload(orderId: orderId, userId: userId, status: "Confirmed"));
        var outboxMock = MakeOutbox(msg);

        OrderStatusChangedNotification? received = null;
        var notifierMock = new Mock<IOrderNotifier>();
        notifierMock.Setup(n => n.NotifyOrderStatusChangedAsync(
                It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()))
            .Callback<OrderStatusChangedNotification, CancellationToken>((n, _) => received = n)
            .Returns(Task.CompletedTask);

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.IsNotNull(received);
        Assert.AreEqual(orderId, received!.OrderId);
        Assert.AreEqual(userId, received.UserId);
        Assert.AreEqual("Confirmed", received.Status);
        Assert.AreEqual(FixedNow, received.OccurredAt);
    }

    // --- Failed dispatch ---

    [TestMethod]
    public async Task ProcessBatch_DispatchThrows_MarkFailedCalledWithBackoff()
    {
        var msg = MakeMessage();
        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier(new Exception("transport error"));

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.IsNull(msg.ProcessedAt);
        Assert.AreEqual(1, msg.AttemptCount);
        Assert.AreEqual(FixedNow, msg.LastAttemptAt);
        Assert.AreEqual("transport error", msg.LastError);
        // first failure: delay = min(5 * 2^0, 300) = 5s
        Assert.AreEqual(FixedNow.AddSeconds(5), msg.NextAttemptAt);
        outboxMock.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessBatch_NullPayload_MarkFailedWithDeserializationError()
    {
        var msg = MakeMessage(payload: "null");
        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier();

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.IsNull(msg.ProcessedAt);
        Assert.AreEqual(1, msg.AttemptCount);
        Assert.AreEqual("Payload deserialization returned null.", msg.LastError);
        notifierMock.Verify(n => n.NotifyOrderStatusChangedAsync(
            It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessBatch_MalformedPayload_MarkFailed()
    {
        var msg = MakeMessage(payload: "{not json");
        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier();

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.IsNull(msg.ProcessedAt);
        Assert.AreEqual(1, msg.AttemptCount);
        Assert.IsNotNull(msg.LastError);
        notifierMock.Verify(n => n.NotifyOrderStatusChangedAsync(
            It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessBatch_LongErrorMessage_IsTruncated()
    {
        var msg = MakeMessage();
        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier(new Exception(new string('x', 1000)));

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.IsNotNull(msg.LastError);
        Assert.AreEqual(500, msg.LastError.Length);
        Assert.EndsWith("...", msg.LastError);
    }

    // --- Exponential backoff ---

    [TestMethod]
    public async Task ProcessBatch_SecondAttemptFailure_BackoffIsDoubled()
    {
        var msg = MakeMessage();
        // Simulate one prior failure (AttemptCount = 1)
        msg.MarkFailed("prior error", FixedNow.AddMinutes(-5), FixedNow.AddSeconds(-1));

        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier(new Exception("still failing"));

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.AreEqual(2, msg.AttemptCount);
        // second failure: delay = min(5 * 2^1, 300) = 10s
        Assert.AreEqual(FixedNow.AddSeconds(10), msg.NextAttemptAt);
    }

    [TestMethod]
    public async Task ProcessBatch_BackoffCappedAtMaximumDelay()
    {
        var msg = MakeMessage();
        // Simulate 6 prior failures → AttemptCount = 6; delay = min(5 * 2^6, 300) = min(320, 300) = 300s
        for (var i = 0; i < 6; i++)
            msg.MarkFailed("error", FixedNow.AddMinutes(-10), FixedNow.AddSeconds(-1));

        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier(new Exception("still failing"));

        var options = new OutboxOptions
        {
            InitialRetryDelaySeconds = 5,
            MaximumRetryDelaySeconds = 300,
            MaximumAttempts = 10,
        };
        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object, options);
        await processor.ProcessBatchAsync();

        // Delay must not exceed the configured max
        Assert.IsNotNull(msg.NextAttemptAt);
        var actualDelaySecs = (msg.NextAttemptAt!.Value - FixedNow).TotalSeconds;
        Assert.IsLessThanOrEqualTo(300.0, actualDelaySecs);
        Assert.AreEqual(FixedNow.AddSeconds(300), msg.NextAttemptAt);
    }

    [TestMethod]
    public async Task ProcessBatch_CustomRetryOptions_AppliedToBackoff()
    {
        var msg = MakeMessage();
        msg.MarkFailed("prior", FixedNow.AddMinutes(-1), FixedNow.AddSeconds(-1));
        msg.MarkFailed("prior", FixedNow.AddMinutes(-1), FixedNow.AddSeconds(-1));

        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier(new Exception("fail"));

        var options = new OutboxOptions { InitialRetryDelaySeconds = 3, MaximumRetryDelaySeconds = 1000 };
        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object, options);
        await processor.ProcessBatchAsync();

        // third failure: delay = min(3 * 2^2, 1000) = 12s
        Assert.AreEqual(3, msg.AttemptCount);
        Assert.AreEqual(FixedNow.AddSeconds(12), msg.NextAttemptAt);
    }

    // --- Maximum attempts ---

    [TestMethod]
    public async Task ProcessBatch_FailureReachingMaximumAttempts_RecordsFinalAttemptWithoutThrowing()
    {
        var msg = MakeMessage();
        for (var i = 0; i < 4; i++)
            msg.MarkFailed("error", FixedNow.AddMinutes(-10), FixedNow.AddSeconds(-1));

        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier(new Exception("final failure"));

        var options = new OutboxOptions { MaximumAttempts = 5 };
        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object, options);
        await processor.ProcessBatchAsync();

        Assert.IsNull(msg.ProcessedAt);
        Assert.AreEqual(5, msg.AttemptCount);
        Assert.AreEqual("final failure", msg.LastError);
        outboxMock.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Unknown event type ---

    [TestMethod]
    public async Task ProcessBatch_UnknownEventType_MarkFailed()
    {
        var msg = MakeMessage(type: "UnknownType", payload: "{}");
        var outboxMock = MakeOutbox(msg);
        var notifierMock = new Mock<IOrderNotifier>();

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.IsNull(msg.ProcessedAt);
        Assert.AreEqual(1, msg.AttemptCount);
        Assert.AreEqual("Unknown event type: UnknownType", msg.LastError);
        Assert.AreEqual(FixedNow.AddSeconds(5), msg.NextAttemptAt);
        notifierMock.Verify(n => n.NotifyOrderStatusChangedAsync(
            It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Mixed batch ---

    [TestMethod]
    public async Task ProcessBatch_OneFailureDoesNotBlockOtherMessages()
    {
        var bad = MakeMessage(type: "UnknownType", payload: "{}");
        var good = MakeMessage();
        var outboxMock = MakeOutbox(bad, good);
        var notifierMock = MakeNotifier();

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        Assert.IsNull(bad.ProcessedAt);
        Assert.AreEqual(1, bad.AttemptCount);
        Assert.AreEqual(FixedNow, good.ProcessedAt);
        notifierMock.Verify(n => n.NotifyOrderStatusChangedAsync(
            It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxMock.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessBatch_RoutesEachTypeToItsHandler()
    {
        var statusMsg = MakeMessage();
        var createdMsg = MakeMessage(type: OutboxEventTypes.OrderCreated, payload: "{\"orderId\":\"x\"}");
        var outboxMock = MakeOutbox(statusMsg, createdMsg);
        var notifierMock = MakeNotifier();
        var busMock = new Mock<IMessageBus>();
        busMock.Setup(b => b.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object, null, null,
            new IntegrationEventOutboxHandler(OutboxEventTypes.OrderCreated, "order-created", busMock.Object));
        await processor.ProcessBatchAsync();

        Assert.AreEqual(FixedNow, statusMsg.ProcessedAt);
        Assert.AreEqual(FixedNow, createdMsg.ProcessedAt);
        notifierMock.Verify(n => n.NotifyOrderStatusChangedAsync(
            It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
        busMock.Verify(b => b.PublishAsync(
            "order-created", OutboxEventTypes.OrderCreated, createdMsg.Payload, createdMsg.Id.ToString(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Cancellation ---

    [TestMethod]
    public async Task ProcessBatch_NotifierThrowsOperationCanceled_PropagatesWithoutMarkingFailed()
    {
        var msg = MakeMessage();
        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier(new OperationCanceledException());

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => processor.ProcessBatchAsync());

        Assert.AreEqual(0, msg.AttemptCount);
        Assert.IsNull(msg.ProcessedAt);
        outboxMock.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessBatch_CancellationRequested_StopsDispatching()
    {
        var msg = MakeMessage();
        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync(cts.Token);

        Assert.IsNull(msg.ProcessedAt);
        notifierMock.Verify(n => n.NotifyOrderStatusChangedAsync(
            It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- SaveAsync always called after processing non-empty batch ---

    [TestMethod]
    public async Task ProcessBatch_AfterProcessing_SaveAsyncAlwaysCalled()
    {
        var msg = MakeMessage();
        var outboxMock = MakeOutbox(msg);
        var notifierMock = MakeNotifier();

        var processor = CreateProcessor(outboxMock.Object, notifierMock.Object);
        await processor.ProcessBatchAsync();

        outboxMock.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Batch size used correctly ---

    [TestMethod]
    public async Task ProcessBatch_UsesBatchSizeFromOptions()
    {
        var outboxMock = MakeOutbox();

        var options = new OutboxOptions { BatchSize = 10, MaximumAttempts = 3 };
        var processor = CreateProcessor(outboxMock.Object, new Mock<IOrderNotifier>().Object, options);
        await processor.ProcessBatchAsync();

        outboxMock.Verify(r => r.GetPendingBatchAsync(
            10, 3, FixedNow, It.IsAny<CancellationToken>()), Times.Once);
    }
}
