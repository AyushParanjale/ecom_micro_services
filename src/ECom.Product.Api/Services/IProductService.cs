namespace ECom.Product.Api.Services;

using ECom.Product.Api.DTOs;

public interface IProductService
{
    Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductResponse>> GetProductsAsync(string? category, CancellationToken cancellationToken = default);
    Task<ProductResponse> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductResponse>> GetProductsByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<ProductResponse> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task InvalidateProductsAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default);
}
