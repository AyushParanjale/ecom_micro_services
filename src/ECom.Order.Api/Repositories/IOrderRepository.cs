namespace ECom.Order.Api.Repositories;

using ECom.Order.Api.Entities;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdWithItemsTrackedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByUserIdWithItemsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetAllWithItemsAsync(CancellationToken cancellationToken = default);
    void Stage(Order order);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
