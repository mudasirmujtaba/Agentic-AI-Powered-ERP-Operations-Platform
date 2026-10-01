using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Sales;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(o => o.Status);
        builder.Property(o => o.Carrier).HasMaxLength(100);
        builder.Property(o => o.TrackingNumber).HasMaxLength(100);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);

        builder.HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Warehouse).WithMany().HasForeignKey(o => o.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("SalesOrderLines");
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
