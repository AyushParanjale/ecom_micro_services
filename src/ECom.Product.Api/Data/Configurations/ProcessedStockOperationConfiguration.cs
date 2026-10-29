namespace ECom.Product.Api.Data.Configurations;

using ECom.Product.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ProcessedStockOperationConfiguration : IEntityTypeConfiguration<ProcessedStockOperation>
{
    public void Configure(EntityTypeBuilder<ProcessedStockOperation> builder)
    {
        builder.ToTable("ProcessedStockOperations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderId).IsRequired();
        builder.Property(o => o.ProcessedAt).IsRequired();

        builder.Property(o => o.Operation)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(o => new { o.OrderId, o.Operation })
            .IsUnique()
            .HasDatabaseName("UX_ProcessedStockOperations_OrderId_Operation");
    }
}
