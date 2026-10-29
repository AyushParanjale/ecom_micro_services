namespace ECom.Product.Api.Services;

using ECom.Product.Api.Entities;
using ECom.Shared.Exceptions;
using ECom.Product.Api.Repositories;
using ECom.Shared.Caching;
using ECom.Product.Api.DTOs;
using ECom.Product.Api.Options;
using Microsoft.Extensions.Options;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repo;
    private readonly ICacheService _cache;
    private readonly CachingOptions _cachingOptions;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository repo,
        ICacheService cache,
        IOptions<CachingOptions> cachingOptions,
        ILogger<ProductService> logger)
    {
        _repo = repo;
        _cache = cache;
        _cachingOptions = cachingOptions.Value;
        _logger = logger;
    }

    public async Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product(
            request.Name.Trim(),
            request.Description.Trim(),
            request.Price,
            request.Category.Trim(),
            request.StockQuantity,
            request.ImageUrl?.Trim());

        await _repo.AddAsync(product, cancellationToken);
        await InvalidateListCacheAsync(cancellationToken);
        return ToResponse(product);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(string? category, CancellationToken cancellationToken = default)
    {
        var listExpiry = TimeSpan.FromMinutes(_cachingOptions.ProductListExpirationMinutes);
        var normalizedCategory = ProductCacheKeys.NormalizeCategory(category);

        // Obtain (or create) the list version token. Invalidation removes this key,
        // causing all subsequent list requests to use a fresh version.
        var version = await _cache.GetOrCreateAsync(
            ProductCacheKeys.VersionKey,
            _ => Task.FromResult(DateTimeOffset.UtcNow.Ticks.ToString()),
            listExpiry,
            cancellationToken);

        var cacheKey = string.IsNullOrEmpty(normalizedCategory)
            ? ProductCacheKeys.ListAll(version)
            : ProductCacheKeys.ListByCategory(version, normalizedCategory);

        var cached = await _cache.GetAsync<IReadOnlyList<ProductResponse>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for product list key {Key}", cacheKey);
            return cached;
        }

        var products = await _repo.GetAvailableAsync(category?.Trim(), cancellationToken);
        var result = products.Select(ToResponse).ToList().AsReadOnly();

        await _cache.SetAsync(cacheKey, result, listExpiry, cancellationToken);
        return result;
    }

    public async Task<ProductResponse> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var detailExpiry = TimeSpan.FromMinutes(_cachingOptions.ProductDetailExpirationMinutes);
        var cacheKey = ProductCacheKeys.Detail(id);

        var cached = await _cache.GetAsync<ProductResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for product detail {ProductId}", id);
            return cached;
        }

        var product = await _repo.GetByIdAsync(id, cancellationToken);
        if (product is null || product.StockQuantity <= 0)
            throw new NotFoundException($"Product not found.");

        var response = ToResponse(product);
        await _cache.SetAsync(cacheKey, response, detailExpiry, cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        // Internal lookup for other services: returns every existing product regardless of stock,
        // straight from the DB so callers always validate against authoritative price/stock.
        var products = await _repo.GetByIdsAsync(ids.Distinct(), cancellationToken);
        return products.Select(ToResponse).ToList().AsReadOnly();
    }

    public async Task<ProductResponse> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _repo.GetByIdAsync(id, cancellationToken);
        if (product is null)
            throw new NotFoundException($"Product not found.");

        product.UpdateDetails(
            request.Name.Trim(),
            request.Description.Trim(),
            request.Price,
            request.Category.Trim(),
            request.ImageUrl?.Trim());

        product.SetStockQuantity(request.StockQuantity);

        await _repo.UpdateAsync(product, cancellationToken);
        await InvalidateProductAsync(id, cancellationToken);
        return ToResponse(product);
    }

    public async Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _repo.GetByIdAsync(id, cancellationToken);
        if (product is null)
            throw new NotFoundException($"Product not found.");

        await _repo.DeleteAsync(id, cancellationToken);
        await InvalidateProductAsync(id, cancellationToken);
    }

    // Called after checkout stock decrements or payment-failure stock restoration
    // for a set of product IDs. Invalidation is best-effort; DB is authoritative.
    public async Task InvalidateProductsAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in productIds)
            await SafeRemoveAsync(ProductCacheKeys.Detail(id), cancellationToken);

        await InvalidateListCacheAsync(cancellationToken);
    }

    private async Task InvalidateProductAsync(Guid id, CancellationToken cancellationToken)
    {
        await SafeRemoveAsync(ProductCacheKeys.Detail(id), cancellationToken);
        await InvalidateListCacheAsync(cancellationToken);
    }

    private async Task InvalidateListCacheAsync(CancellationToken cancellationToken)
    {
        // Remove the version token. All list keys referencing the old version become
        // unreachable; they expire naturally via absolute TTL.
        await SafeRemoveAsync(ProductCacheKeys.VersionKey, cancellationToken);
    }

    private async Task SafeRemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            // Cache invalidation failure must never roll back a committed DB change.
            _logger.LogWarning(ex, "Cache removal failed for key {Key}; entries will expire via absolute TTL.", key);
        }
    }

    private static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.Category, p.StockQuantity, p.ImageUrl);
}
