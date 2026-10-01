using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Ai;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Security;
using OpsPilot.Domain.Ai;

namespace OpsPilot.Api.Controllers;

/// <summary>AI gateway: the only way the Angular app and the Python agents reach each other.</summary>
[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController(IAiCopilotService copilot, IAiSqlGateway sql, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("chat")]
    public Task<AiChatResponse> Chat(AiChatRequest request, CancellationToken cancellationToken) =>
        copilot.ChatAsync(request, BearerToken(), cancellationToken);

    [HttpGet("conversations")]
    public Task<IReadOnlyList<AiConversationSummaryDto>> Conversations(CancellationToken cancellationToken) =>
        copilot.ListConversationsAsync(cancellationToken);

    [HttpGet("conversations/{id:guid}")]
    public Task<AiConversationDto> Conversation(Guid id, CancellationToken cancellationToken) =>
        copilot.GetConversationAsync(id, cancellationToken);

    [HttpGet("actions")]
    public Task<IReadOnlyList<AiActionDto>> Actions([FromQuery] AiActionStatus? status, CancellationToken cancellationToken) =>
        copilot.ListActionsAsync(status, cancellationToken);

    [HttpGet("actions/{id:guid}")]
    public Task<AiActionDto> Action(Guid id, CancellationToken cancellationToken) => copilot.GetActionAsync(id, cancellationToken);

    /// <summary>Approving creates purchase order drafts, so it needs the same role as creating them by hand.</summary>
    [HttpPost("actions/{id:guid}/approve")]
    [Authorize(Policy = Policies.ManagePurchaseOrders)]
    public Task<AiActionDto> Approve(Guid id, DecideAiActionRequest request, CancellationToken cancellationToken) =>
        copilot.ApproveAsync(id, request, BearerToken(), cancellationToken);

    [HttpPost("actions/{id:guid}/reject")]
    [Authorize(Policy = Policies.ManagePurchaseOrders)]
    public Task<AiActionDto> Reject(Guid id, DecideAiActionRequest request, CancellationToken cancellationToken) =>
        copilot.RejectAsync(id, request, BearerToken(), cancellationToken);

    /// <summary>Read-only query tool used by the ERP Query agent on behalf of the signed-in user.</summary>
    [HttpPost("sql")]
    public Task<AiSqlResult> Sql(AiSqlRequest request, CancellationToken cancellationToken) =>
        sql.ExecuteAsync(request.Sql, currentUser.Roles, cancellationToken);

    [HttpGet("schema")]
    public Task<IReadOnlyList<AiViewSchema>> Schema(CancellationToken cancellationToken) =>
        sql.GetSchemaAsync(currentUser.Roles, cancellationToken);

    private string BearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : string.Empty;
    }
}
