using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Service;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class ServiceTicketConfiguration : IEntityTypeConfiguration<ServiceTicket>
{
    public void Configure(EntityTypeBuilder<ServiceTicket> builder)
    {
        builder.ToTable("ServiceTickets");
        builder.Property(t => t.TicketNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(t => t.TicketNumber).IsUnique();
        builder.Property(t => t.Subject).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(4000).IsRequired();
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedToUserId);
        builder.Property(t => t.AssignedToName).HasMaxLength(200);
        builder.Property(t => t.Resolution).HasMaxLength(2000);
        builder.Property(t => t.AiSummary).HasMaxLength(4000);
        builder.Ignore(t => t.IsOpen);

        builder.HasOne(t => t.Customer).WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.SalesOrder).WithMany().HasForeignKey(t => t.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(t => t.Comments).WithOne().HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.ToTable("TicketComments");
        builder.Property(c => c.AuthorName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Body).HasMaxLength(4000).IsRequired();
    }
}
