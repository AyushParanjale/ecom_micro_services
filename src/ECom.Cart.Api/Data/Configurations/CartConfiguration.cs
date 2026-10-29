namespace ECom.Cart.Api.Data.Configurations;

using ECom.Cart.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(c => c.Id);

        // UserId is the JWT 'sub' issued by the Auth service: a plain value, not a foreign key.
        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(c => c.UserId)
            .IsUnique()
            .HasDatabaseName("UX_Carts_UserId");

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(ci => ci.CartId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Items)
            .HasField("_items");
    }
}
