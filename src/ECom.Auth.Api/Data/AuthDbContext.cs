namespace ECom.Auth.Api.Data;

using ECom.Auth.Api.Data.Configurations;
using ECom.Auth.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public sealed class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new ApplicationUserConfiguration());
    }
}
