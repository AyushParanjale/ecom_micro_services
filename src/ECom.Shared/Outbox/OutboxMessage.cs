namespace ECom.Shared.Outbox;

// Infrastructure-only persistence entity. Not a domain aggregate.
// Never expose through public APIs. Never store secrets in Payload or LastError.
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public OutboxMessage(string type, string payload, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
        CreatedAt = occurredAt;
        AttemptCount = 0;
    }

    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
    }

    public void MarkFailed(string? errorMessage, DateTimeOffset now, DateTimeOffset nextAttemptAt)
    {
        AttemptCount++;
        LastAttemptAt = now;
        // Truncate to avoid storing large stack traces
        const int maxErrorLength = 500;
        LastError = errorMessage is { Length: > maxErrorLength }
            ? string.Concat(errorMessage.AsSpan(0, maxErrorLength - 3), "...")
            : errorMessage;
        NextAttemptAt = nextAttemptAt;
    }
}
