namespace ECom.Order.Api.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer("Server=.\\SQLEXPRESS;Database=ECommerce_Order_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;

        return new OrderDbContext(options);
    }
}
