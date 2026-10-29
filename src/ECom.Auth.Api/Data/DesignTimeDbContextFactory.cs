namespace ECom.Auth.Api.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=.\\SQLEXPRESS;Database=ECommerce_Auth_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;

        return new AuthDbContext(options);
    }
}
