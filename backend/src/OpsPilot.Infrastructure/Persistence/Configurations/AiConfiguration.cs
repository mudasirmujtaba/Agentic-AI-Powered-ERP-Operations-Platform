using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Ai;
using OpsPilot.Domain.Audit;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(l => l.UserEmail).HasMaxLength(256);
        builder.Property(l => l.Action).HasMaxLength(100).IsRequired();
        builder.Property(l => l.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(l => l.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(l => l.Source).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(l => l.OccurredAtUtc);
        builder.HasIndex(l => new { l.EntityType, l.EntityId });
    }
}

public class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => new { c.UserId, c.LastMessageAtUtc });
        builder.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("AiMessages");
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Content).IsRequired();
    }
}

public class AiActionConfiguration : IEntityTypeConfiguration<AiAction>
{
    public void Configure(EntityTypeBuilder<AiAction> builder)
    {
        builder.Property(a => a.RequestedByEmail).HasMaxLength(256);
        builder.Property(a => a.Agent).HasMaxLength(50).IsRequired();
        builder.Property(a => a.ActionType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(500).IsRequired();
        builder.Property(a => a.InputJson).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ThreadId).HasMaxLength(64).IsRequired();
        builder.HasIndex(a => a.Status);
        builder.HasOne<AiConversation>().WithMany().HasForeignKey(a => a.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Approvals).WithOne().HasForeignKey(p => p.ActionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiApprovalConfiguration : IEntityTypeConfiguration<AiApproval>
{
    public void Configure(EntityTypeBuilder<AiApproval> builder)
    {
        builder.ToTable("AiApprovals");
        builder.Property(p => p.DecidedByEmail).HasMaxLength(256);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Comments).HasMaxLength(1000);
    }
}
