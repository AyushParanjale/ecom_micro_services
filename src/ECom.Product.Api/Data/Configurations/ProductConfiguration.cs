namespace ECom.Product.Api.Data.Configurations;

using ECom.Product.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.Category)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Price)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.StockQuantity)
            .IsRequired();

        builder.Property(p => p.ImageUrl)
            .HasMaxLength(500);

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.HasIndex(p => p.Category)
            .HasDatabaseName("IX_Products_Category");

        builder.HasIndex(p => p.Name)
            .HasDatabaseName("IX_Products_Name");
    }
}
