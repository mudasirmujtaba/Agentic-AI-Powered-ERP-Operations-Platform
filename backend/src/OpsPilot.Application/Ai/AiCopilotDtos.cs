using System.Text.Json;
using OpsPilot.Domain.Ai;

namespace OpsPilot.Application.Ai;

public record AiChatRequest(Guid? ConversationId, string Message);

public record AiConversationSummaryDto(Guid Id, string Title, DateTime LastMessageAtUtc);

public record AiActionDecisionDto(string Status, string? DecidedByEmail, DateTime DecidedAtUtc, string? Comments);

public record AiActionDto(
    Guid Id,
    Guid ConversationId,
    string Agent,
    string ActionType,
    string Summary,
    AiActionStatus Status,
    JsonElement Input,
    JsonElement? Output,
    string? RequestedByEmail,
    DateTime CreatedAtUtc,
    AiActionDecisionDto? Decision);

public record AiMessageDto(Guid Id, AiMessageRole Role, string Content, JsonElement? Metadata, DateTime CreatedAtUtc, AiActionDto? Action);

public record AiConversationDto(Guid Id, string Title, IReadOnlyList<AiMessageDto> Messages);

public record AiChatResponse(Guid ConversationId, string Title, IReadOnlyList<AiMessageDto> Messages);

/// <summary>Approvers may change quantities before approving ("Modify"); a quantity of 0 drops the line.</summary>
public record AiActionLineOverride(Guid ProductId, int Quantity);

public record DecideAiActionRequest(string? Comments, IReadOnlyList<AiActionLineOverride>? Lines);

/// <summary>Payload of a <see cref="AiActionTypes.CreatePurchaseOrders"/> proposal, one draft PO per supplier.</summary>
public record PurchaseProposal(IReadOnlyList<ProposedPurchaseOrder> Orders, string? Rationale);

public record ProposedPurchaseOrder(Guid SupplierId, string SupplierName, Guid WarehouseId, string? WarehouseCode, IReadOnlyList<ProposedPurchaseLine> Lines);

public record ProposedPurchaseLine(Guid ProductId, string ProductCode, string? ProductName, int Quantity, decimal UnitCost, string? Reason);
