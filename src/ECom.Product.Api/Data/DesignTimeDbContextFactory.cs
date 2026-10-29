namespace ECom.Product.Api.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProductDbContext>
{
    public ProductDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseSqlServer("Server=.\\SQLEXPRESS;Database=ECommerce_Product_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;

        return new ProductDbContext(options);
    }
}
