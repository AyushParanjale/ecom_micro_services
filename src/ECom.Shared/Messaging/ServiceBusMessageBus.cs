namespace ECom.Shared.Messaging;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;

/// <summary>
/// Singleton publisher: one <see cref="ServiceBusClient"/> (AMQP connection) for the process and one
/// cached <see cref="ServiceBusSender"/> per queue/topic.
/// </summary>
public sealed class ServiceBusMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusMessageBus(ServiceBusClient client) => _client = client;

    public Task PublishAsync(string entityName, string subject, string jsonPayload, string? messageId = null, CancellationToken cancellationToken = default)
    {
        var sender = _senders.GetOrAdd(entityName, _client.CreateSender);
        var message = new ServiceBusMessage(BinaryData.FromString(jsonPayload))
        {
            MessageId = messageId ?? Guid.NewGuid().ToString(),
            CorrelationId = Activity.Current?.TraceId.ToString(),
            Subject = subject,
            ContentType = "application/json"
        };
        return sender.SendMessageAsync(message, cancellationToken);
    }

    public Task PublishAsync<T>(T message, string entityName, string? messageId = null, CancellationToken cancellationToken = default)
        where T : notnull
        => PublishAsync(entityName, message.GetType().Name, JsonSerializer.Serialize(message, message.GetType(), EComJson.Options), messageId, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
            await sender.DisposeAsync();
        _senders.Clear();
    }
}

/// <summary>
/// Registered when <c>ServiceBus:ConnectionString</c> is not configured. Publishing fails, so outbox
/// rows stay pending and are retried once the connection string is supplied.
/// </summary>
internal sealed class UnconfiguredMessageBus : IMessageBus
{
    public Task PublishAsync(string entityName, string subject, string jsonPayload, string? messageId = null, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            $"Azure Service Bus is not configured ('{ServiceBusOptions.SectionName}:ConnectionString'); cannot publish to '{entityName}'.");

    public Task PublishAsync<T>(T message, string entityName, string? messageId = null, CancellationToken cancellationToken = default)
        where T : notnull
        => PublishAsync(entityName, typeof(T).Name, string.Empty, messageId, cancellationToken);
}
