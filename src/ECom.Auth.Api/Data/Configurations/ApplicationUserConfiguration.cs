namespace ECom.Auth.Api.Data.Configurations;

using ECom.Auth.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}
