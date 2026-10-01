using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Purchasing;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.Property(o => o.PoNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(o => o.PoNumber).IsUnique();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(o => o.Status);
        builder.Property(o => o.RejectionReason).HasMaxLength(500);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);

        builder.HasOne(o => o.Supplier).WithMany().HasForeignKey(o => o.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Warehouse).WithMany().HasForeignKey(o => o.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines");
        builder.Property(l => l.UnitCost).HasPrecision(18, 2);
        builder.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
