using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Finance;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.InvoiceNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(i => i.Status);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.AmountPaid).HasPrecision(18, 2);

        builder.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.SalesOrder).WithMany().HasForeignKey(i => i.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.Payments).WithOne().HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.Property(l => l.Description).HasMaxLength(300).IsRequired();
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Reference).HasMaxLength(100);
    }
}
