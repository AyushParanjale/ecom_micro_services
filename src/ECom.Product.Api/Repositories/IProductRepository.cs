namespace ECom.Product.Api.Repositories;

using ECom.Product.Api.Entities;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetAvailableAsync(string? category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByIdsTrackedAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
