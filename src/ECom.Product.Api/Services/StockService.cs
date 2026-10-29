namespace ECom.Product.Api.Services;

using ECom.Product.Api.DTOs;
using ECom.Product.Api.Entities;
using ECom.Product.Api.Repositories;
using ECom.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

public interface IStockService
{
    /// <summary>
    /// All-or-nothing stock decrement for a checkout. Idempotent per <paramref name="orderId"/>.
    /// </summary>
    Task DecrementAsync(Guid orderId, IReadOnlyList<StockLine> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compensation: puts back the stock taken by <paramref name="orderId"/>. Idempotent, and a
    /// no-op if no decrement was recorded for the order.
    /// </summary>
    Task RestoreAsync(Guid orderId, IReadOnlyList<StockLine> items, CancellationToken cancellationToken = default);
}

public sealed class StockService : IStockService
{
    private readonly IProductRepository _productRepo;
    private readonly IStockOperationRepository _operationRepo;
    private readonly IProductService _productService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StockService> _logger;

    public StockService(
        IProductRepository productRepo,
        IStockOperationRepository operationRepo,
        IProductService productService,
        TimeProvider timeProvider,
        ILogger<StockService> logger)
    {
        _productRepo = productRepo;
        _operationRepo = operationRepo;
        _productService = productService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task DecrementAsync(Guid orderId, IReadOnlyList<StockLine> items, CancellationToken cancellationToken = default)
    {
        var lines = Normalize(orderId, items);

        if (await _operationRepo.ExistsAsync(orderId, StockOperationType.Decrement, cancellationToken))
        {
            _logger.LogInformation("Stock already decremented for OrderId={OrderId}; skipping.", orderId);
            return;
        }

        if (await _operationRepo.ExistsAsync(orderId, StockOperationType.Restore, cancellationToken))
            throw new ConflictException(
                "The checkout for this order was already cancelled.",
                "Order compensated");

        var products = await _productRepo.GetByIdsTrackedAsync(lines.Select(l => l.ProductId), cancellationToken);
        var productMap = products.ToDictionary(p => p.Id);

        if (lines.Any(l => !productMap.ContainsKey(l.ProductId)))
            throw new ConflictException(
                "A product in the cart is no longer available.",
                "Product unavailable");

        if (lines.Any(l => productMap[l.ProductId].StockQuantity < l.Quantity))
            throw new ConflictException(
                "One or more products do not have enough stock to fulfill the order.",
                "Insufficient stock");

        foreach (var line in lines)
            productMap[line.ProductId].DecrementStock(line.Quantity);

        _operationRepo.Stage(new ProcessedStockOperation(orderId, StockOperationType.Decrement, _timeProvider.GetUtcNow()));

        if (!await TrySaveAsync(orderId, StockOperationType.Decrement, cancellationToken))
            return;

        _logger.LogInformation("Decremented stock for OrderId={OrderId} ({Count} products)", orderId, lines.Count);
        await InvalidateAsync(lines, cancellationToken);
    }

    public async Task RestoreAsync(Guid orderId, IReadOnlyList<StockLine> items, CancellationToken cancellationToken = default)
    {
        var lines = Normalize(orderId, items);

        if (await _operationRepo.ExistsAsync(orderId, StockOperationType.Restore, cancellationToken))
        {
            _logger.LogInformation("Stock already restored for OrderId={OrderId}; skipping.", orderId);
            return;
        }

        var wasDecremented = await _operationRepo.ExistsAsync(orderId, StockOperationType.Decrement, cancellationToken);

        if (wasDecremented)
        {
            var products = await _productRepo.GetByIdsTrackedAsync(lines.Select(l => l.ProductId), cancellationToken);
            var productMap = products.ToDictionary(p => p.Id);

            foreach (var line in lines)
            {
                if (productMap.TryGetValue(line.ProductId, out var product))
                    product.RestoreStock(line.Quantity);
            }
        }
        else
        {
            // Record a tombstone so a late-arriving decrement for this order is refused.
            _logger.LogWarning("No stock decrement recorded for OrderId={OrderId}; nothing to restore.", orderId);
        }

        _operationRepo.Stage(new ProcessedStockOperation(orderId, StockOperationType.Restore, _timeProvider.GetUtcNow()));

        if (!await TrySaveAsync(orderId, StockOperationType.Restore, cancellationToken))
            return;

        if (wasDecremented)
        {
            _logger.LogInformation("Restored stock for OrderId={OrderId} ({Count} products)", orderId, lines.Count);
            await InvalidateAsync(lines, cancellationToken);
        }
    }

    // Returns false when a concurrent request already recorded the same operation (unique index).
    private async Task<bool> TrySaveAsync(Guid orderId, StockOperationType operation, CancellationToken cancellationToken)
    {
        try
        {
            await _productRepo.SaveAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            if (await _operationRepo.ExistsAsync(orderId, operation, cancellationToken))
            {
                _logger.LogInformation("Concurrent {Operation} for OrderId={OrderId} already applied.", operation, orderId);
                return false;
            }
            throw;
        }
    }

    private async Task InvalidateAsync(IEnumerable<StockLine> lines, CancellationToken cancellationToken)
    {
        // Best-effort cache invalidation; DB is authoritative if this fails.
        try
        {
            await _productService.InvalidateProductsAsync(lines.Select(l => l.ProductId), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed after stock change; entries will expire via TTL.");
        }
    }

    private static List<StockLine> Normalize(Guid orderId, IReadOnlyList<StockLine>? items)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("OrderId is required.");
        if (items is null || items.Count == 0)
            throw new DomainException("At least one item is required.");
        if (items.Any(i => i.ProductId == Guid.Empty))
            throw new DomainException("ProductId is required.");
        if (items.Any(i => i.Quantity <= 0))
            throw new DomainException("Quantity must be greater than zero.");

        return items
            .GroupBy(i => i.ProductId)
            .Select(g => new StockLine(g.Key, g.Sum(i => i.Quantity)))
            .ToList();
    }
}
