using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Insights;
using OpsPilot.Domain.Notifications;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(4000).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(300);
        builder.Property(n => n.Source).HasMaxLength(60).IsRequired();
        builder.Property(n => n.Severity).HasConversion<string>().HasMaxLength(20);
        // The bell's query: my newest notifications, and my unread count.
        builder.HasIndex(n => new { n.UserId, n.CreatedAtUtc });
        builder.HasIndex(n => new { n.UserId, n.ReadAtUtc });
    }
}

public class InsightReportConfiguration : IEntityTypeConfiguration<InsightReport>
{
    public void Configure(EntityTypeBuilder<InsightReport> builder)
    {
        builder.Property(r => r.Kind).HasMaxLength(40).IsRequired();
        builder.Property(r => r.Summary).IsRequired();
        builder.Property(r => r.DataJson).IsRequired();
        builder.HasIndex(r => new { r.Kind, r.GeneratedAtUtc });
    }
}
