namespace ECom.Order.Api.Data.Configurations;

using ECom.Order.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId).IsRequired();
        builder.Property(o => o.OrderDate).IsRequired();

        builder.Property(o => o.TotalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        builder.HasIndex(o => o.UserId)
            .HasDatabaseName("IX_Orders_UserId");

        builder.HasIndex(o => o.OrderDate)
            .HasDatabaseName("IX_Orders_OrderDate");

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(oi => oi.OrderId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
