using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Catalog;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Code).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.UnitPrice).HasPrecision(18, 2);
        builder.Property(p => p.Cost).HasPrecision(18, 2);

        builder.HasIndex(p => p.Code).IsUnique();

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.PrimarySupplier)
            .WithMany()
            .HasForeignKey(p => p.PrimarySupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
