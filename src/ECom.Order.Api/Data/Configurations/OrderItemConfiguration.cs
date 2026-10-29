namespace ECom.Order.Api.Data.Configurations;

using ECom.Order.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(oi => oi.Id);

        builder.Property(oi => oi.OrderId).IsRequired();
        builder.Property(oi => oi.ProductId).IsRequired();

        builder.Property(oi => oi.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(oi => oi.UnitPrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(oi => oi.Quantity).IsRequired();

        builder.Ignore(oi => oi.LineTotal);

        builder.HasIndex(oi => oi.OrderId)
            .HasDatabaseName("IX_OrderItems_OrderId");
    }
}
