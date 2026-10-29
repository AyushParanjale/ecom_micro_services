namespace ECom.Shared.Outbox;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class OutboxProcessor : IOutboxProcessor
{
    private readonly IOutboxRepository _outboxRepo;
    private readonly IReadOnlyDictionary<string, IOutboxMessageHandler> _handlers;
    private readonly OutboxOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IOutboxRepository outboxRepo,
        IEnumerable<IOutboxMessageHandler> handlers,
        IOptions<OutboxOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxProcessor> logger)
    {
        _outboxRepo = outboxRepo;
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.Ordinal);
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var messages = await _outboxRepo.GetPendingBatchAsync(
            _options.BatchSize,
            _options.MaximumAttempts,
            now,
            cancellationToken);

        if (messages.Count == 0) return;

        _logger.LogDebug("Processing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await ProcessMessageAsync(message, cancellationToken);
        }

        await _outboxRepo.SaveAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(message.Type, out var handler))
        {
            _logger.LogWarning(
                "Unknown outbox event type {Type} for message {MessageId}", message.Type, message.Id);
            MarkFailed(message, $"Unknown event type: {message.Type}");
            return;
        }

        try
        {
            await handler.HandleAsync(message, cancellationToken);
            message.MarkProcessed(_timeProvider.GetUtcNow());

            _logger.LogInformation(
                "Processed outbox message {MessageId} Type={Type}", message.Id, message.Type);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to process outbox message {MessageId} Type={Type} Attempt={Attempt}",
                message.Id, message.Type, message.AttemptCount + 1);
            MarkFailed(message, ex.Message);
        }
    }

    private void MarkFailed(OutboxMessage message, string error)
    {
        var now = _timeProvider.GetUtcNow();
        // Bounded exponential backoff: delay = min(initial * 2^attempt, max)
        var delaySecs = Math.Min(
            _options.InitialRetryDelaySeconds * Math.Pow(2, message.AttemptCount),
            _options.MaximumRetryDelaySeconds);
        message.MarkFailed(error, now, now.AddSeconds(delaySecs));

        if (message.AttemptCount >= _options.MaximumAttempts)
        {
            _logger.LogError(
                "Outbox message {MessageId} Type={Type} has reached maximum attempts ({Max}) and will not be retried automatically.",
                message.Id, message.Type, _options.MaximumAttempts);
        }
    }
}
