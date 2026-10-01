using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.Code).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ContactName).HasMaxLength(150);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.Phone).HasMaxLength(50);

        builder.HasIndex(s => s.Code).IsUnique();
    }
}
