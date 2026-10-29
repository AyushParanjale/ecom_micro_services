namespace ECom.Product.Api.Data;

using ECom.Product.Api.Data.Configurations;
using ECom.Product.Api.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProcessedStockOperation> ProcessedStockOperations => Set<ProcessedStockOperation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new ProductConfiguration());
        builder.ApplyConfiguration(new ProcessedStockOperationConfiguration());
    }
}
