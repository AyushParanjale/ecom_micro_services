namespace ECom.Product.Api.Repositories;

using ECom.Product.Api.Data;
using ECom.Product.Api.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class StockOperationRepository : IStockOperationRepository
{
    private readonly ProductDbContext _context;

    public StockOperationRepository(ProductDbContext context) => _context = context;

    public Task<bool> ExistsAsync(Guid orderId, StockOperationType operation, CancellationToken cancellationToken = default) =>
        _context.ProcessedStockOperations
            .AsNoTracking()
            .AnyAsync(o => o.OrderId == orderId && o.Operation == operation, cancellationToken);

    public void Stage(ProcessedStockOperation operation) =>
        _context.ProcessedStockOperations.Add(operation);
}
