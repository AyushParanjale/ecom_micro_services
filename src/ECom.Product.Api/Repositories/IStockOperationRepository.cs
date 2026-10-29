namespace ECom.Product.Api.Repositories;

using ECom.Product.Api.Entities;

public interface IStockOperationRepository
{
    Task<bool> ExistsAsync(Guid orderId, StockOperationType operation, CancellationToken cancellationToken = default);

    /// <summary>Adds the record to the current unit of work (committed by <see cref="IProductRepository.SaveAsync"/>).</summary>
    void Stage(ProcessedStockOperation operation);
}
