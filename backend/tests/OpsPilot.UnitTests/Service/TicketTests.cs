using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Ai;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Service;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Identity;
using OpsPilot.Domain.Service;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.UnitTests.TestSupport;

namespace OpsPilot.UnitTests.Service;

public class TicketTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeAiClient _ai = new();
    private readonly Guid _customerId;
    private readonly Guid _agentId;

    public TicketTests()
    {
        using var db = _database.NewContext();
        var customer = new Customer { Code = "CUST-1", Name = "Apex Manufacturing" };
        var agent = new ApplicationUser { UserName = "ivy", Email = "ivy@opspilot.local", FirstName = "Ivy", LastName = "Chen" };
        db.Customers.Add(customer);
        db.Users.Add(agent);
        db.SaveChanges();
        _customerId = customer.Id;
        _agentId = agent.Id;
    }

    private ApplicationDbContext Db() => _database.NewContext();

    private TicketService Tickets(ApplicationDbContext db) => new(db, new AuditLogWriter(db, _database.CurrentUser),
        _database.CurrentUser, _ai, new SaveTicketRequestValidator(), new ChangeTicketStatusRequestValidator(),
        new AddTicketCommentRequestValidator());

    private SaveTicketRequest Request(Guid? assignee = null, TicketPriority priority = TicketPriority.High) =>
        new("X200 leaking", "Seal drips under load.", _customerId, null, null, TicketCategory.ProductDefect, priority, assignee);

    [Fact]
    public async Task Create_numbers_sequentially_and_resolves_the_assignee_name()
    {
        await using var db = Db();
        var first = await Tickets(db).CreateAsync(Request(_agentId));
        var second = await Tickets(db).CreateAsync(Request());

        Assert.Equal("TCK-50001", first.TicketNumber);
        Assert.Equal("TCK-50002", second.TicketNumber);
        Assert.Equal("Ivy Chen", first.AssignedToName);
        Assert.Equal(TicketStatus.Open, first.Status);
        Assert.Contains(TicketStatus.InProgress, first.AllowedNextStatuses);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "CreateTicket"));
    }

    [Fact]
    public async Task Unknown_assignee_is_a_business_rule_violation()
    {
        await using var db = Db();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Tickets(db).CreateAsync(Request(Guid.NewGuid())));
    }

    [Fact]
    public async Task Resolving_requires_a_resolution_and_reopening_clears_it()
    {
        await using var db = Db();
        var service = Tickets(db);
        var ticket = await service.CreateAsync(Request());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(TicketStatus.Resolved, null)));

        var resolved = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(TicketStatus.Resolved, "Replaced seal"));
        Assert.Equal("Replaced seal", resolved.Resolution);
        Assert.NotNull(resolved.ResolvedAtUtc);

        var reopened = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(TicketStatus.InProgress, null));
        Assert.Null(reopened.Resolution);
        Assert.Null(reopened.ResolvedAtUtc);
    }

    [Fact]
    public async Task Invalid_transition_and_comments_on_closed_tickets_are_rejected()
    {
        await using var db = Db();
        var service = Tickets(db);
        var ticket = await service.CreateAsync(Request());

        // Open -> Closed skips resolution.
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(TicketStatus.Closed, null)));

        await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(TicketStatus.Resolved, "Done"));
        await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(TicketStatus.Closed, null));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AddCommentAsync(ticket.Id, new AddTicketCommentRequest("One more thing", false)));
    }

    [Fact]
    public async Task Comments_record_the_author_and_appear_in_order()
    {
        await using var db = Db();
        var service = Tickets(db);
        var ticket = await service.CreateAsync(Request());

        await service.AddCommentAsync(ticket.Id, new AddTicketCommentRequest("Asked for photos", false));
        var updated = await service.AddCommentAsync(ticket.Id, new AddTicketCommentRequest("Batch B-0412", true));

        Assert.Equal(["Asked for photos", "Batch B-0412"], updated.Comments.Select(c => c.Body));
        Assert.All(updated.Comments, c => Assert.Equal("Test User", c.AuthorName));
        Assert.True(updated.Comments[1].IsInternal);
    }

    [Fact]
    public async Task Empty_comment_fails_validation()
    {
        await using var db = Db();
        var ticket = await Tickets(db).CreateAsync(Request());
        await Assert.ThrowsAsync<ValidationException>(() =>
            Tickets(db).AddCommentAsync(ticket.Id, new AddTicketCommentRequest("  ", false)));
    }

    [Fact]
    public async Task List_filters_open_tickets_assigned_to_me()
    {
        _database.CurrentUser.UserId = _agentId;
        await using var db = Db();
        var service = Tickets(db);
        var mine = await service.CreateAsync(Request(_agentId));
        await service.CreateAsync(Request());
        var done = await service.CreateAsync(Request(_agentId));
        await service.ChangeStatusAsync(done.Id, new ChangeTicketStatusRequest(TicketStatus.Resolved, "Fixed"));

        var result = await service.ListAsync(new TicketQuery { AssignedToMe = true, OpenOnly = true });
        var stats = await service.GetStatsAsync();

        Assert.Equal([mine.TicketNumber], result.Items.Select(t => t.TicketNumber));
        Assert.Equal(new TicketStatsDto(Open: 2, Urgent: 0, Unassigned: 1, AssignedToMe: 1, ResolvedLast30Days: 1), stats);
    }

    [Fact]
    public async Task Summary_sends_the_history_to_the_ai_and_is_stored()
    {
        await using var db = Db();
        var service = Tickets(db);
        var ticket = await service.CreateAsync(Request());
        await service.AddCommentAsync(ticket.Id, new AddTicketCommentRequest("Seal batch B-0412", true));

        var summarised = await service.SummarizeAsync(ticket.Id);

        Assert.Equal("summary of TCK-50001", summarised.AiSummary);
        Assert.Single(_ai.LastSummaryInput!.History);
        Assert.Equal("ProductDefect", _ai.LastSummaryInput.Category);
    }

    public void Dispose() => _database.Dispose();

    private sealed class FakeAiClient : IAiAgentClient
    {
        public TicketSummaryInput? LastSummaryInput { get; private set; }

        public Task<AgentReply> ChatAsync(AgentChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgentReply> ResumeAsync(AgentResumeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AiTextReply> SummarizeTicketAsync(TicketSummaryInput input, CancellationToken cancellationToken = default)
        {
            LastSummaryInput = input;
            return Task.FromResult(new AiTextReply($"summary of {input.TicketNumber}", null));
        }

        public Task<AiTextReply> FindRecurringProblemsAsync(TicketInsightsInput input, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiTextReply($"{input.Tickets.Count} tickets", (JsonElement?)null));
    }
}
