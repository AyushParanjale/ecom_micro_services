namespace ECom.Order.Api.Repositories;

using ECom.Order.Api.Entities;
using ECom.Order.Api.Data;
using Microsoft.EntityFrameworkCore;

public sealed class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public OrderRepository(OrderDbContext context) => _context = context;

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<Order?> GetByIdWithItemsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<Order?> GetByIdWithItemsTrackedAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetAllWithItemsAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ToListAsync(cancellationToken);
        return [.. orders.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)];
    }

    public async Task<IReadOnlyList<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Order>> GetByUserIdWithItemsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .ToListAsync(cancellationToken);
        return [.. orders.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)];
    }

    public async Task<IReadOnlyList<Order>> GetByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default) =>
        await _context.Orders
            .AsNoTracking()
            .Where(o => o.Status == status)
            .ToListAsync(cancellationToken);

    public void Stage(Order order) =>
        _context.Orders.Add(order);

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
