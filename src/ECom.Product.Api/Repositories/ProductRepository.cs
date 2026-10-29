namespace ECom.Product.Api.Repositories;

using ECom.Product.Api.Data;
using ECom.Product.Api.Entities;
using ECom.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

public sealed class ProductRepository : IProductRepository
{
    private readonly ProductDbContext _context;

    public ProductRepository(ProductDbContext context) => _context = context;

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetAvailableAsync(string? category, CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.StockQuantity > 0);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var trimmed = category.Trim();
            query = query.Where(p => p.Category.ToLower() == trimmed.ToLower());
        }

        return await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _context.Products
            .AsNoTracking()
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsTrackedAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _context.Products
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Carts and orders live in other services, so there are no FK references to guard here:
    // carts drop missing products on read and orders keep their own name/price snapshots.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' not found.");

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
