using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Ai;

public class AiConversation : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime LastMessageAtUtc { get; set; }
    public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}

public class AiMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public AiMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>Structured extras for the UI and observability: intent, data table, citations, SQL, node trace, token usage, action id.</summary>
    public string? MetadataJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public enum AiMessageRole
{
    User,
    Assistant
}
