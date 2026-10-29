namespace ECom.Shared.Outbox;

public sealed record OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollingIntervalSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 25;
    public int MaximumAttempts { get; init; } = 5;
    public int InitialRetryDelaySeconds { get; init; } = 5;
    public int MaximumRetryDelaySeconds { get; init; } = 300;
}

/// <summary>Dispatches outbox rows of a single <see cref="Type"/> (e.g. SignalR push or Service Bus publish).</summary>
public interface IOutboxMessageHandler
{
    string Type { get; }

    /// <summary>Throw to mark the row failed; it is retried with exponential backoff.</summary>
    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public interface IOutboxProcessor
{
    Task ProcessBatchAsync(CancellationToken cancellationToken = default);
}
