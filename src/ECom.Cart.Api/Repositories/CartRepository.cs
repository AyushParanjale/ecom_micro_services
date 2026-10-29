namespace ECom.Cart.Api.Repositories;

using ECom.Cart.Api.Entities;
using ECom.Cart.Api.Data;
using Microsoft.EntityFrameworkCore;

public sealed class CartRepository : ICartRepository
{
    private readonly CartDbContext _context;

    public CartRepository(CartDbContext context) => _context = context;

    public async Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public async Task<Cart?> GetByUserIdWithItemsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public async Task<Cart?> GetByIdAsync(Guid cartId, CancellationToken cancellationToken = default) =>
        await _context.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cartId, cancellationToken);

    public async Task AddAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        await _context.Carts.AddAsync(cart, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(cart).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            _context.Carts.Update(cart);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var cart = await _context.Carts.FindAsync([cartId], cancellationToken);
        if (cart is not null)
        {
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public void StageItem(CartItem item) => _context.CartItems.Add(item);

    public async Task SaveAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
