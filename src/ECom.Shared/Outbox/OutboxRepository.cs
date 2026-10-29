namespace ECom.Shared.Outbox;

using Microsoft.EntityFrameworkCore;

public interface IOutboxRepository
{
    /// <summary>Adds the message to the current unit of work; it is persisted by the caller's SaveChanges.</summary>
    void Stage(OutboxMessage message);
    Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int batchSize, int maxAttempts, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outbox repository over any service DbContext that maps <see cref="OutboxMessage"/>.</summary>
public sealed class OutboxRepository<TContext> : IOutboxRepository where TContext : DbContext
{
    private readonly TContext _context;

    public OutboxRepository(TContext context) => _context = context;

    public void Stage(OutboxMessage message) =>
        _context.Set<OutboxMessage>().Add(message);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(
        int batchSize,
        int maxAttempts,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // The DB does the bulk filter (ProcessedAt, AttemptCount); ordering, backoff and Take are
        // applied client-side so the query stays provider-agnostic for DateTimeOffset comparisons.
        var pending = await _context.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.AttemptCount < maxAttempts)
            .ToListAsync(cancellationToken);

        return pending
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .Where(m => m.NextAttemptAt == null || m.NextAttemptAt <= now)
            .Take(batchSize)
            .ToList();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
