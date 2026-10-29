namespace ECom.Order.Api.Repositories;

using ECom.Order.Api.Entities;
using ECom.Order.Api.Data;
using Microsoft.EntityFrameworkCore;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly OrderDbContext _context;

    public PaymentRepository(OrderDbContext context) => _context = context;

    public async Task<Payment?> GetByIdForUserAsync(Guid paymentId, Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId, cancellationToken);

    public async Task<Payment?> GetByOrderIdForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.UserId == userId, cancellationToken);

    public async Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

    public async Task<Payment?> GetByIdTrackedAsync(Guid paymentId, CancellationToken cancellationToken = default) =>
        await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

    public void Stage(Payment payment) =>
        _context.Payments.Add(payment);

    public async Task SaveAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
