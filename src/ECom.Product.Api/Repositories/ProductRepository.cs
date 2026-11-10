namespace ECom.Product.Api.Repositories;

using ECom.Product.Api.Data;
using ECom.Product.Api.DTOs;
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

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetAvailablePageAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var products = _context.Products
            .AsNoTracking()
            .Where(p => p.StockQuantity > 0);

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim().ToLower();
            products = products.Where(p => p.Category.ToLower() == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            products = products.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Description.ToLower().Contains(term) ||
                p.Category.ToLower().Contains(term));
        }

        if (query.MinPrice is not null)
            products = products.Where(p => p.Price >= query.MinPrice);

        if (query.MaxPrice is not null)
            products = products.Where(p => p.Price <= query.MaxPrice);

        var totalCount = await products.CountAsync(cancellationToken);

        var ordered = (query.SortByPrice, query.SortDescending) switch
        {
            (true, false) => products.OrderBy(p => p.Price).ThenBy(p => p.Name),
            (true, true) => products.OrderByDescending(p => p.Price).ThenBy(p => p.Name),
            (false, true) => products.OrderByDescending(p => p.Name),
            _ => products.OrderBy(p => p.Name),
        };

        var items = await ordered
            .ThenBy(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<string>> GetAvailableCategoriesAsync(CancellationToken cancellationToken = default) =>
        await _context.Products
            .AsNoTracking()
            .Where(p => p.StockQuantity > 0)
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

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
