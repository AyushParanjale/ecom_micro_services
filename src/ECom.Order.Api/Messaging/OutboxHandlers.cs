namespace ECom.Order.Api.Messaging;

using System.Text.Json;
using ECom.Order.Api.DTOs;
using ECom.Order.Api.Services;
using ECom.Shared.Messaging;
using ECom.Shared.Outbox;

/// <summary>Delivers <see cref="OutboxEventTypes.OrderStatusChanged"/> rows to the order owner via SignalR.</summary>
public sealed class OrderStatusChangedOutboxHandler : IOutboxMessageHandler
{
    private readonly IOrderNotifier _notifier;

    public OrderStatusChangedOutboxHandler(IOrderNotifier notifier) => _notifier = notifier;

    public string Type => OutboxEventTypes.OrderStatusChanged;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var notification = JsonSerializer.Deserialize<OrderStatusChangedNotification>(message.Payload)
            ?? throw new InvalidOperationException("Payload deserialization returned null.");

        await _notifier.NotifyOrderStatusChangedAsync(notification, cancellationToken);
    }
}

/// <summary>
/// Publishes an outbox row's JSON payload to an Azure Service Bus topic. The outbox row id is used
/// as the Service Bus MessageId so duplicate publishes can be detected.
/// </summary>
public sealed class IntegrationEventOutboxHandler : IOutboxMessageHandler
{
    private readonly string _entityName;
    private readonly IMessageBus _messageBus;

    public IntegrationEventOutboxHandler(string type, string entityName, IMessageBus messageBus)
    {
        Type = type;
        _entityName = entityName;
        _messageBus = messageBus;
    }

    public string Type { get; }

    public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        => _messageBus.PublishAsync(_entityName, Type, message.Payload, message.Id.ToString(), cancellationToken);
}
