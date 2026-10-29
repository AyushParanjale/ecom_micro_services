namespace ECom.Cart.Api.Data;

using ECom.Cart.Api.Data.Configurations;
using ECom.Cart.Api.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class CartDbContext : DbContext
{
    public CartDbContext(DbContextOptions<CartDbContext> options) : base(options) { }

    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new CartConfiguration());
        builder.ApplyConfiguration(new CartItemConfiguration());
    }
}
