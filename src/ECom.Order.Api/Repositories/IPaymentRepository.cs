namespace ECom.Order.Api.Repositories;

using ECom.Order.Api.Entities;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdForUserAsync(Guid paymentId, Guid userId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByOrderIdForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdTrackedAsync(Guid paymentId, CancellationToken cancellationToken = default);
    void Stage(Payment payment);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
