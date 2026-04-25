using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Enums;

namespace OrderProcessing.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(o => o.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()   // Store as string for readability in DB
            .HasMaxLength(50);

        builder.Property(o => o.TotalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(o => o.IdempotencyKey)
            .HasMaxLength(128);

        builder.Property(o => o.FailureReason)
            .HasMaxLength(500);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .IsRequired();

        // Unique index on IdempotencyKey to enforce at DB level (handles race conditions in idempotency check)
        builder.HasIndex(o => o.IdempotencyKey)
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        builder.HasIndex(o => o.CustomerEmail);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);

        // Configure the private backing field for Items collection
        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tell EF Core about the private backing field
        builder.Navigation(o => o.Items).HasField("_items");

        // Ignore domain events — they are transient and not persisted
        builder.Ignore(o => o.DomainEvents);
    }
}