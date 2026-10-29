namespace ECom.Order.Api.Data;

using ECom.Order.Api.Data.Configurations;
using ECom.Order.Api.Entities;
using ECom.Shared.Outbox;
using Microsoft.EntityFrameworkCore;

public sealed class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new OrderConfiguration());
        builder.ApplyConfiguration(new OrderItemConfiguration());
        builder.ApplyConfiguration(new PaymentConfiguration());
        builder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
