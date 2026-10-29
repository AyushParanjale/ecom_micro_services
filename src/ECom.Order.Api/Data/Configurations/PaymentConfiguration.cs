namespace ECom.Order.Api.Data.Configurations;

using ECom.Order.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.OrderId).IsRequired();
        builder.Property(p => p.UserId).IsRequired();

        builder.Property(p => p.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(p => p.ProviderReference)
            .HasMaxLength(200);

        builder.Property(p => p.FailureReason)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.HasIndex(p => p.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UQ_Payments_IdempotencyKey");

        builder.HasIndex(p => p.OrderId)
            .IsUnique()
            .HasDatabaseName("UQ_Payments_OrderId");

        builder.HasIndex(p => p.UserId)
            .HasDatabaseName("IX_Payments_UserId");

        builder.HasOne<Order>()
            .WithOne()
            .HasForeignKey<Payment>(p => p.OrderId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
