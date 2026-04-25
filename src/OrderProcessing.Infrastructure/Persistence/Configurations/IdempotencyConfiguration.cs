using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Infrastructure.Persistence.Configurations;

public class IdempotencyConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Key)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(i => i.RequestHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(i => i.ResponsePayload)
            .HasColumnType("nvarchar(max)");

        builder.Property(i => i.Status)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .IsRequired();

        // Unique index on Key to ensure atomic insert semantics
        builder.HasIndex(i => i.Key).IsUnique();
    }
}
