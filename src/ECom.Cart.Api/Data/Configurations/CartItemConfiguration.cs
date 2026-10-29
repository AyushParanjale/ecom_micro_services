namespace ECom.Cart.Api.Data.Configurations;

using ECom.Cart.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");
        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.CartId).IsRequired();
        // ProductId references the Product service by value; live product data is fetched over HTTP.
        builder.Property(ci => ci.ProductId).IsRequired();
        builder.Property(ci => ci.Quantity).IsRequired();
        builder.Property(ci => ci.CreatedAt).IsRequired();
        builder.Property(ci => ci.UpdatedAt).IsRequired();

        builder.HasIndex(ci => new { ci.CartId, ci.ProductId })
            .IsUnique()
            .HasDatabaseName("UX_CartItems_CartId_ProductId");
    }
}
