namespace ECom.Shared.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.OccurredAt)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.ProcessedAt)
            .IsRequired(false);

        builder.Property(m => m.AttemptCount)
            .IsRequired();

        builder.Property(m => m.LastAttemptAt)
            .IsRequired(false);

        builder.Property(m => m.LastError)
            .IsRequired(false)
            .HasMaxLength(503); // 500 chars + "..." truncation

        builder.Property(m => m.NextAttemptAt)
            .IsRequired(false);

        builder.HasIndex(m => m.ProcessedAt);
        builder.HasIndex(m => m.NextAttemptAt);
        builder.HasIndex(m => new { m.ProcessedAt, m.NextAttemptAt, m.OccurredAt });
    }
}
