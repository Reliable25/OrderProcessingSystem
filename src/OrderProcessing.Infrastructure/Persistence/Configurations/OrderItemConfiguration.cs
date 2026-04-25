using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductId)
            .IsRequired();

        builder.Property(i => i.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.UnitPrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.Quantity)
            .IsRequired();

        // TotalPrice is computed — do not map to a column
        builder.Ignore(i => i.TotalPrice);

        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.ProductId);
    }
}