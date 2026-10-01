using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Inventory;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.HasIndex(i => new { i.ProductId, i.WarehouseId }).IsUnique();
        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Warehouse).WithMany().HasForeignKey(i => i.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_InventoryItems_Quantities",
            "[QuantityOnHand] >= 0 AND [QuantityReserved] >= 0 AND [QuantityReserved] <= [QuantityOnHand]"));
    }
}

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Reference).HasMaxLength(100);
        builder.Property(t => t.Notes).HasMaxLength(500);
        builder.HasIndex(t => new { t.ProductId, t.OccurredAtUtc });
        builder.HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Warehouse).WithMany().HasForeignKey(t => t.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}
