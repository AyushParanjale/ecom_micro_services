namespace ECom.Cart.Api.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CartDbContext>
{
    public CartDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseSqlServer("Server=.\\SQLEXPRESS;Database=ECommerce_Cart_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;

        return new CartDbContext(options);
    }
}
