using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Purchasing;
using OpsPilot.Domain.Ai;
using OpsPilot.Domain.Audit;
using OpsPilot.Domain.Common;

namespace OpsPilot.Application.Ai;

public interface IAiCopilotService
{
    Task<AiChatResponse> ChatAsync(AiChatRequest request, string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConversationSummaryDto>> ListConversationsAsync(CancellationToken cancellationToken = default);
    Task<AiConversationDto> GetConversationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiActionDto>> ListActionsAsync(AiActionStatus? status, CancellationToken cancellationToken = default);
    Task<AiActionDto> GetActionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AiActionDto> ApproveAsync(Guid id, DecideAiActionRequest request, string accessToken, CancellationToken cancellationToken = default);
    Task<AiActionDto> RejectAsync(Guid id, DecideAiActionRequest request, string accessToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// The AI gateway: persists conversations, relays turns to the LangGraph service, records proposed actions, and — only after
/// a person approves — executes them through the normal application services so every business rule still applies.
/// </summary>
public class AiCopilotService(
    IApplicationDbContext db,
    IAiAgentClient agent,
    IPurchaseOrderService purchaseOrders,
    ICurrentUserService currentUser,
    AuditLogWriter audit,
    IValidator<AiChatRequest> chatValidator,
    ILogger<AiCopilotService> logger) : IAiCopilotService
{
    private const int HistoryMessages = 8;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, string accessToken, CancellationToken cancellationToken = default)
    {
        await chatValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = RequireUser();
        var now = DateTime.UtcNow;

        var conversation = request.ConversationId is { } id
            ? await db.AiConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id, cancellationToken)
              ?? throw new NotFoundException(nameof(AiConversation), id)
            : new AiConversation { UserId = user.Id, Title = Truncate(request.Message.Trim(), 80) };

        if (request.ConversationId is null)
        {
            db.AiConversations.Add(conversation);
        }

        var history = request.ConversationId is null
            ? []
            : await db.AiConversations
                .Where(c => c.Id == conversation.Id)
                .SelectMany(c => c.Messages)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Take(HistoryMessages)
                .Select(m => new AgentHistoryMessage(m.Role == AiMessageRole.User ? "user" : "assistant", m.Content))
                .ToListAsync(cancellationToken);
        history.Reverse();

        var userMessage = new AiMessage { Role = AiMessageRole.User, Content = request.Message.Trim(), CreatedAtUtc = now };
        conversation.Messages.Add(userMessage);
        conversation.LastMessageAtUtc = now;

        var threadId = Guid.NewGuid().ToString("N");
        AgentReply reply;
        try
        {
            reply = await agent.ChatAsync(new AgentChatRequest(threadId, userMessage.Content, history, user, accessToken), cancellationToken);
        }
        catch (AiServiceUnavailableException ex)
        {
            logger.LogWarning(ex, "AI service unavailable");
            reply = new AgentReply(
                "The AI service isn't available right now, so I can't answer that. The rest of OpsPilot keeps working; please try again shortly.",
                "error", null, JsonSerializer.SerializeToElement(new { error = ex.Message }, Json));
        }

        AiAction? action = null;
        if (reply.Proposal is { } proposal)
        {
            action = new AiAction
            {
                ConversationId = conversation.Id,
                RequestedBy = user.Id,
                RequestedByEmail = user.Email,
                Agent = proposal.Agent,
                ActionType = proposal.ActionType,
                Summary = Truncate(proposal.Summary, 500),
                InputJson = proposal.Payload.GetRawText(),
                ThreadId = threadId,
            };
            db.AiActions.Add(action);
            audit.Record("ProposeAiAction", "AiAction", action.Id.ToString(), $"{proposal.Agent} proposed: {action.Summary}", AuditSource.AiAssisted);
        }

        var assistantMessage = new AiMessage
        {
            Role = AiMessageRole.Assistant,
            Content = reply.Content,
            CreatedAtUtc = DateTime.UtcNow,
            MetadataJson = BuildMetadata(reply.Metadata, reply.Intent, action?.Id),
        };
        conversation.Messages.Add(assistantMessage);
        conversation.LastMessageAtUtc = assistantMessage.CreatedAtUtc;

        await db.SaveChangesAsync(cancellationToken);

        var actionDto = action is null ? null : await GetActionAsync(action.Id, cancellationToken);
        return new AiChatResponse(conversation.Id, conversation.Title,
        [
            ToDto(userMessage, null),
            ToDto(assistantMessage, actionDto),
        ]);
    }

    public async Task<IReadOnlyList<AiConversationSummaryDto>> ListConversationsAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUser().Id;
        return await db.AiConversations.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastMessageAtUtc)
            .Take(50)
            .Select(c => new AiConversationSummaryDto(c.Id, c.Title, c.LastMessageAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<AiConversationDto> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser().Id;
        var conversation = await db.AiConversations.AsNoTracking()
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(AiConversation), id);

        var actions = (await LoadActionDtosAsync(db.AiActions.Where(a => a.ConversationId == id), cancellationToken))
            .ToDictionary(a => a.Id);

        var messages = conversation.Messages
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => ToDto(m, ActionIdOf(m) is { } actionId ? actions.GetValueOrDefault(actionId) : null))
            .ToList();

        return new AiConversationDto(conversation.Id, conversation.Title, messages);
    }

    public async Task<IReadOnlyList<AiActionDto>> ListActionsAsync(AiActionStatus? status, CancellationToken cancellationToken = default)
    {
        var actions = db.AiActions.AsQueryable();
        if (status is { } s) actions = actions.Where(a => a.Status == s);
        return await LoadActionDtosAsync(actions, cancellationToken);
    }

    public async Task<AiActionDto> GetActionAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadActionDtosAsync(db.AiActions.Where(a => a.Id == id), cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException(nameof(AiAction), id);

    public async Task<AiActionDto> ApproveAsync(Guid id, DecideAiActionRequest request, string accessToken, CancellationToken cancellationToken = default)
    {
        var user = RequireUser();
        var action = await LoadActionAsync(id, cancellationToken);
        if (action.ActionType != AiActionTypes.CreatePurchaseOrders)
        {
            throw new BusinessRuleException($"Unsupported action type {action.ActionType}.");
        }

        var proposal = ApplyOverrides(Deserialize(action), request.Lines);
        action.InputJson = JsonSerializer.Serialize(proposal, Json);
        action.Approve(user.Id, user.Email, request.Comments?.Trim(), DateTime.UtcNow);
        audit.Record("ApproveAiAction", "AiAction", action.Id.ToString(), $"Approved AI proposal: {action.Summary}", AuditSource.AiAssisted,
            new { request.Comments, modified = request.Lines is { Count: > 0 } });
        await db.SaveChangesAsync(cancellationToken);

        // Execute through the same service the UI uses: supplier/product/warehouse checks, numbering, audit.
        var created = new List<object>();
        try
        {
            foreach (var order in proposal.Orders.Where(o => o.Lines.Count > 0))
            {
                var po = await purchaseOrders.CreateAsync(new SavePurchaseOrderRequest(
                    order.SupplierId,
                    order.WarehouseId,
                    null,
                    Truncate($"Drafted from an OpsPilot AI recommendation approved by {user.Email}.", 1000),
                    order.Lines.Select(l => new PurchaseOrderLineRequest(l.ProductId, l.Quantity, l.UnitCost)).ToList()),
                    cancellationToken);
                created.Add(new { po.Id, po.PoNumber, po.SupplierName, po.TotalAmount, po.RequiresApproval });
            }

            action.MarkExecuted(JsonSerializer.Serialize(new { purchaseOrders = created }, Json));
            audit.Record("ExecuteAiAction", "AiAction", action.Id.ToString(),
                $"Created {created.Count} purchase order draft(s) from an approved AI recommendation", AuditSource.AiAssisted, created);
        }
        catch (Exception ex) when (ex is BusinessRuleException or ValidationException or NotFoundException or ConflictException)
        {
            action.MarkFailed(JsonSerializer.Serialize(new { error = ex.Message, purchaseOrders = created }, Json));
            audit.Record("ExecuteAiAction", "AiAction", action.Id.ToString(), $"Execution failed: {ex.Message}", AuditSource.AiAssisted);
        }

        await db.SaveChangesAsync(cancellationToken);
        await ResumeAgentAsync(action, "approved", request.Comments, accessToken, cancellationToken);
        return await GetActionAsync(id, cancellationToken);
    }

    public async Task<AiActionDto> RejectAsync(Guid id, DecideAiActionRequest request, string accessToken, CancellationToken cancellationToken = default)
    {
        var user = RequireUser();
        var action = await LoadActionAsync(id, cancellationToken);
        action.Reject(user.Id, user.Email, request.Comments?.Trim(), DateTime.UtcNow);
        audit.Record("RejectAiAction", "AiAction", action.Id.ToString(), $"Rejected AI proposal: {action.Summary}", AuditSource.AiAssisted,
            new { request.Comments });
        await db.SaveChangesAsync(cancellationToken);

        await ResumeAgentAsync(action, "rejected", request.Comments, accessToken, cancellationToken);
        return await GetActionAsync(id, cancellationToken);
    }

    /// <summary>Lets the paused graph finish its turn; the ERP side is already committed, so failures here only cost the follow-up message.</summary>
    private async Task ResumeAgentAsync(AiAction action, string decision, string? comments, string accessToken, CancellationToken cancellationToken)
    {
        var outcome = action.OutputJson is null
            ? JsonSerializer.SerializeToElement(new { }, Json)
            : JsonDocument.Parse(action.OutputJson).RootElement.Clone();

        string content;
        JsonElement? metadata;
        try
        {
            var reply = await agent.ResumeAsync(
                new AgentResumeRequest(action.ThreadId, decision, comments, outcome, RequireUser(), accessToken), cancellationToken);
            content = reply.Content;
            metadata = reply.Metadata;
        }
        catch (AiServiceUnavailableException ex)
        {
            logger.LogWarning(ex, "Could not resume agent thread {ThreadId}", action.ThreadId);
            content = action.Status switch
            {
                AiActionStatus.Executed => "The recommendation was approved and the purchase order drafts were created.",
                AiActionStatus.Failed => "The recommendation was approved but could not be executed; see the action details.",
                _ => "The recommendation was rejected; nothing was changed.",
            };
            metadata = null;
        }

        var conversation = await db.AiConversations.FirstAsync(c => c.Id == action.ConversationId, cancellationToken);
        var message = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiMessageRole.Assistant,
            Content = content,
            CreatedAtUtc = DateTime.UtcNow,
            MetadataJson = BuildMetadata(metadata, "approval_outcome", action.Id),
        };
        conversation.Messages.Add(message);
        conversation.LastMessageAtUtc = message.CreatedAtUtc;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AiAction> LoadActionAsync(Guid id, CancellationToken cancellationToken) =>
        await db.AiActions.Include(a => a.Approvals).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(AiAction), id);

    private static PurchaseProposal Deserialize(AiAction action) =>
        JsonSerializer.Deserialize<PurchaseProposal>(action.InputJson, Json)
        ?? throw new BusinessRuleException("The proposal payload is empty.");

    private static PurchaseProposal ApplyOverrides(PurchaseProposal proposal, IReadOnlyList<AiActionLineOverride>? overrides)
    {
        if (overrides is not { Count: > 0 })
        {
            return proposal;
        }

        var quantities = overrides.ToDictionary(o => o.ProductId, o => o.Quantity);
        if (quantities.Values.Any(q => q < 0))
        {
            throw new BusinessRuleException("Quantities cannot be negative.");
        }

        var orders = proposal.Orders
            .Select(o => o with
            {
                Lines = o.Lines
                    .Select(l => quantities.TryGetValue(l.ProductId, out var q) ? l with { Quantity = q } : l)
                    .Where(l => l.Quantity > 0)
                    .ToList(),
            })
            .Where(o => o.Lines.Count > 0)
            .ToList();

        if (orders.Count == 0)
        {
            throw new BusinessRuleException("Every line was set to zero; reject the recommendation instead.");
        }

        return proposal with { Orders = orders };
    }

    private async Task<List<AiActionDto>> LoadActionDtosAsync(IQueryable<AiAction> query, CancellationToken cancellationToken)
    {
        var actions = await query.AsNoTracking()
            .Include(a => a.Approvals)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return actions.Select(a =>
        {
            var decision = a.Approvals.OrderByDescending(p => p.DecidedAtUtc).FirstOrDefault();
            return new AiActionDto(
                a.Id, a.ConversationId, a.Agent, a.ActionType, a.Summary, a.Status,
                JsonDocument.Parse(a.InputJson).RootElement.Clone(),
                a.OutputJson is null ? null : JsonDocument.Parse(a.OutputJson).RootElement.Clone(),
                a.RequestedByEmail,
                a.CreatedAtUtc,
                decision is null
                    ? null
                    : new AiActionDecisionDto(decision.Status.ToString(),
                        decision.DecidedByEmail,
                        decision.DecidedAtUtc, decision.Comments));
        }).ToList();
    }

    private AgentUser RequireUser()
    {
        var id = currentUser.UserId ?? throw new UnauthorizedAccessException();
        return new AgentUser(id, currentUser.Email ?? string.Empty, currentUser.Name ?? currentUser.Email ?? string.Empty, currentUser.Roles);
    }

    private static string BuildMetadata(JsonElement? metadata, string? intent, Guid? actionId)
    {
        var dictionary = metadata is { ValueKind: JsonValueKind.Object } m
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(m.GetRawText(), Json) ?? new()
            : new Dictionary<string, JsonElement>();

        if (intent is not null) dictionary["intent"] = JsonSerializer.SerializeToElement(intent, Json);
        if (actionId is not null) dictionary["actionId"] = JsonSerializer.SerializeToElement(actionId, Json);
        return JsonSerializer.Serialize(dictionary, Json);
    }

    private static Guid? ActionIdOf(AiMessage message)
    {
        if (message.MetadataJson is null) return null;
        using var document = JsonDocument.Parse(message.MetadataJson);
        return document.RootElement.TryGetProperty("actionId", out var value) && value.TryGetGuid(out var id) ? id : null;
    }

    private static AiMessageDto ToDto(AiMessage message, AiActionDto? action) => new(
        message.Id,
        message.Role,
        message.Content,
        message.MetadataJson is null ? null : JsonDocument.Parse(message.MetadataJson).RootElement.Clone(),
        message.CreatedAtUtc,
        action);

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..(length - 1)] + "…";
}

public class AiChatRequestValidator : AbstractValidator<AiChatRequest>
{
    public AiChatRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
    }
}
