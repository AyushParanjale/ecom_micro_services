namespace ECom.Cart.Api.Repositories;

using ECom.Cart.Api.Entities;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Cart?> GetByUserIdWithItemsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Cart?> GetByIdAsync(Guid cartId, CancellationToken cancellationToken = default);
    Task AddAsync(Cart cart, CancellationToken cancellationToken = default);
    Task UpdateAsync(Cart cart, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid cartId, CancellationToken cancellationToken = default);
    void StageItem(CartItem item);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
