namespace ECom.Shared.Messaging;

public interface IMessageBus
{
    /// <summary>Publishes an already-serialized JSON payload to a queue or topic.</summary>
    /// <param name="subject">Message type name, used by consumers for routing/versioning.</param>
    /// <param name="messageId">Stable id (e.g. the outbox row id) so broker-side duplicate detection works.</param>
    Task PublishAsync(string entityName, string subject, string jsonPayload, string? messageId = null, CancellationToken cancellationToken = default);

    /// <summary>Serializes <paramref name="message"/> with <see cref="EComJson.Options"/> and publishes it.</summary>
    Task PublishAsync<T>(T message, string entityName, string? messageId = null, CancellationToken cancellationToken = default)
        where T : notnull;
}

public interface IIntegrationEventHandler<in TMessage>
{
    /// <summary>
    /// Handles one message. Must be idempotent (delivery is at-least-once) and must let exceptions
    /// propagate so Service Bus retries and eventually dead-letters the message.
    /// </summary>
    Task HandleAsync(TMessage message, string messageId, CancellationToken cancellationToken = default);
}
