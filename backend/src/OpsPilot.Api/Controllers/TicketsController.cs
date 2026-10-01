using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Service;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
public class TicketsController(ITicketService tickets) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<TicketListItemDto>> List([FromQuery] TicketQuery query, CancellationToken cancellationToken) =>
        tickets.ListAsync(query, cancellationToken);

    [HttpGet("stats")]
    public Task<TicketStatsDto> Stats(CancellationToken cancellationToken) =>
        tickets.GetStatsAsync(cancellationToken);

    [HttpGet("assignees")]
    public Task<IReadOnlyList<TicketAssigneeDto>> Assignees(CancellationToken cancellationToken) =>
        tickets.ListAssigneesAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<TicketDto> Get(Guid id, CancellationToken cancellationToken) =>
        tickets.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageTickets)]
    public async Task<ActionResult<TicketDto>> Create(SaveTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await tickets.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, ticket);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageTickets)]
    public Task<TicketDto> Update(Guid id, SaveTicketRequest request, CancellationToken cancellationToken) =>
        tickets.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Policies.ManageTickets)]
    public Task<TicketDto> ChangeStatus(Guid id, ChangeTicketStatusRequest request, CancellationToken cancellationToken) =>
        tickets.ChangeStatusAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/comments")]
    [Authorize(Policy = Policies.ManageTickets)]
    public Task<TicketDto> AddComment(Guid id, AddTicketCommentRequest request, CancellationToken cancellationToken) =>
        tickets.AddCommentAsync(id, request, cancellationToken);

    /// <summary>AI summary of the ticket and its history (design doc §13); stored on the ticket.</summary>
    [HttpPost("{id:guid}/summary")]
    [Authorize(Policy = Policies.ManageTickets)]
    public Task<TicketDto> Summarize(Guid id, CancellationToken cancellationToken) =>
        tickets.SummarizeAsync(id, cancellationToken);

    /// <summary>AI analysis of recent tickets for recurring problems (design doc §13).</summary>
    [HttpPost("insights")]
    public Task<TicketInsightsDto> Insights([FromQuery] int days = 90, CancellationToken cancellationToken = default) =>
        tickets.FindRecurringProblemsAsync(days, cancellationToken);
}
