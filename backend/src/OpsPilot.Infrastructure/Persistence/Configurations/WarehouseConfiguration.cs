using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Inventory;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.Property(w => w.Code).HasMaxLength(30).IsRequired();
        builder.Property(w => w.Name).HasMaxLength(150).IsRequired();
        builder.Property(w => w.Location).HasMaxLength(250);

        builder.HasIndex(w => w.Code).IsUnique();
    }
}
