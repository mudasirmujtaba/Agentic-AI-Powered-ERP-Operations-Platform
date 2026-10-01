using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Ai;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Audit;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Identity;
using OpsPilot.Domain.Service;

namespace OpsPilot.Application.Service;

public interface ITicketService
{
    Task<PagedResult<TicketListItemDto>> ListAsync(TicketQuery query, CancellationToken cancellationToken = default);
    Task<TicketStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<TicketDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TicketDto> CreateAsync(SaveTicketRequest request, CancellationToken cancellationToken = default);
    Task<TicketDto> UpdateAsync(Guid id, SaveTicketRequest request, CancellationToken cancellationToken = default);
    Task<TicketDto> ChangeStatusAsync(Guid id, ChangeTicketStatusRequest request, CancellationToken cancellationToken = default);
    Task<TicketDto> AddCommentAsync(Guid id, AddTicketCommentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TicketAssigneeDto>> ListAssigneesAsync(CancellationToken cancellationToken = default);
    Task<TicketDto> SummarizeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TicketInsightsDto> FindRecurringProblemsAsync(int days, CancellationToken cancellationToken = default);
}

public class TicketService(
    IApplicationDbContext db,
    AuditLogWriter audit,
    ICurrentUserService currentUser,
    IAiAgentClient ai,
    IValidator<SaveTicketRequest> saveValidator,
    IValidator<ChangeTicketStatusRequest> statusValidator,
    IValidator<AddTicketCommentRequest> commentValidator) : ITicketService
{
    /// <summary>Cap on tickets sent to the model for recurring-problem analysis, newest first.</summary>
    private const int MaxInsightTickets = 80;

    private static readonly Dictionary<string, Expression<Func<ServiceTicket, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ticketNumber"] = t => t.TicketNumber,
            ["subject"] = t => t.Subject,
            ["customerName"] = t => t.Customer.Name,
            ["priority"] = t => t.Priority,
            ["status"] = t => t.Status,
            ["createdAtUtc"] = t => t.CreatedAtUtc,
        };

    public async Task<PagedResult<TicketListItemDto>> ListAsync(TicketQuery query, CancellationToken cancellationToken = default)
    {
        var tickets = db.ServiceTickets.AsNoTracking();

        if (query.Status is { } status) tickets = tickets.Where(t => t.Status == status);
        if (query.Priority is { } priority) tickets = tickets.Where(t => t.Priority == priority);
        if (query.Category is { } category) tickets = tickets.Where(t => t.Category == category);
        if (query.CustomerId is { } customerId) tickets = tickets.Where(t => t.CustomerId == customerId);
        if (query.OpenOnly) tickets = tickets.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
        if (query.AssignedToMe) tickets = tickets.Where(t => t.AssignedToUserId == currentUser.UserId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            tickets = tickets.Where(t =>
                t.TicketNumber.Contains(search) || t.Subject.Contains(search) || t.Customer.Name.Contains(search) ||
                (t.Product != null && t.Product.Code.Contains(search)));
        }

        // Default order: the newest first. Priority ordering is available as a sort column.
        query.SortBy ??= "createdAtUtc";
        query.SortDirection ??= "desc";

        return await tickets
            .ApplySort(query, SortMap, "createdAtUtc")
            .Select(t => new TicketListItemDto(
                t.Id, t.TicketNumber, t.Subject, t.Customer.Name, t.Product == null ? null : t.Product.Code, t.Category, t.Priority, t.Status,
                t.AssignedToName, t.Comments.Count, t.CreatedAtUtc, t.UpdatedAtUtc))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<TicketStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var open = db.ServiceTickets.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);

        return new TicketStatsDto(
            await open.CountAsync(cancellationToken),
            await open.CountAsync(t => t.Priority == TicketPriority.Urgent, cancellationToken),
            await open.CountAsync(t => t.AssignedToUserId == null, cancellationToken),
            await open.CountAsync(t => t.AssignedToUserId == currentUser.UserId, cancellationToken),
            await db.ServiceTickets.CountAsync(t => t.ResolvedAtUtc >= since, cancellationToken));
    }

    public async Task<TicketDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await db.ServiceTickets.AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.SalesOrder)
            .Include(t => t.Product)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceTicket), id);

        return ToDto(ticket);
    }

    public async Task<TicketDto> CreateAsync(SaveTicketRequest request, CancellationToken cancellationToken = default)
    {
        await saveValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureReferencesAsync(request, cancellationToken);

        var ticket = new ServiceTicket
        {
            TicketNumber = await DocumentNumbers.NextAsync(db.ServiceTickets.Select(t => t.TicketNumber), "TCK-", 50001, cancellationToken),
        };
        Apply(ticket, request);
        ticket.Assign(request.AssignedToUserId, await AssigneeNameAsync(request.AssignedToUserId, cancellationToken));

        db.ServiceTickets.Add(ticket);
        audit.Record("CreateTicket", "ServiceTicket", ticket.TicketNumber, $"Opened {ticket.TicketNumber}: {ticket.Subject} ({ticket.Priority})");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(ticket.Id, cancellationToken);
    }

    public async Task<TicketDto> UpdateAsync(Guid id, SaveTicketRequest request, CancellationToken cancellationToken = default)
    {
        await saveValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await LoadAsync(id, cancellationToken);
        await EnsureReferencesAsync(request, cancellationToken);

        var previousAssignee = ticket.AssignedToUserId;
        var previousPriority = ticket.Priority;
        Apply(ticket, request);
        if (previousAssignee != request.AssignedToUserId)
        {
            ticket.Assign(request.AssignedToUserId, await AssigneeNameAsync(request.AssignedToUserId, cancellationToken));
            audit.Record("AssignTicket", "ServiceTicket", ticket.TicketNumber,
                $"Assigned {ticket.TicketNumber} to {ticket.AssignedToName ?? "nobody"}");
        }
        if (previousPriority != ticket.Priority)
        {
            audit.Record("UpdateTicket", "ServiceTicket", ticket.TicketNumber,
                $"Changed {ticket.TicketNumber} priority from {previousPriority} to {ticket.Priority}");
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TicketDto> ChangeStatusAsync(Guid id, ChangeTicketStatusRequest request, CancellationToken cancellationToken = default)
    {
        await statusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await LoadAsync(id, cancellationToken);

        var previous = ticket.Status;
        ticket.TransitionTo(request.Status, DateTime.UtcNow, request.Resolution);
        audit.Record("ChangeTicketStatus", "ServiceTicket", ticket.TicketNumber,
            $"Moved {ticket.TicketNumber} from {previous} to {ticket.Status}");

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TicketDto> AddCommentAsync(Guid id, AddTicketCommentRequest request, CancellationToken cancellationToken = default)
    {
        await commentValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await LoadAsync(id, cancellationToken);

        ticket.AddComment(request.Body, currentUser.UserId, currentUser.Name ?? currentUser.Email ?? "Unknown user",
            request.IsInternal, DateTime.UtcNow);
        // Touch the header so "last updated" reflects the new activity.
        ticket.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<TicketAssigneeDto>> ListAssigneesAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Email != SystemAccounts.AutomationEmail)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(cancellationToken);

        return users
            .Select(u => new TicketAssigneeDto(u.Id, DisplayName(u.FirstName, u.LastName, u.Email), u.Email ?? string.Empty))
            .OrderBy(u => u.Name)
            .ToList();
    }

    public async Task<TicketDto> SummarizeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await db.ServiceTickets
            .Include(t => t.Customer)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceTicket), id);

        var input = new TicketSummaryInput(
            ticket.TicketNumber, ticket.Subject, ticket.Description, ticket.Customer.Name,
            ticket.Category.ToString(), ticket.Priority.ToString(), ticket.Status.ToString(), ticket.Resolution,
            ticket.Comments.OrderBy(c => c.CreatedAtUtc)
                .Select(c => new TicketHistoryEntry(c.AuthorName, c.Body, c.IsInternal, c.CreatedAtUtc))
                .ToList());

        var reply = await ai.SummarizeTicketAsync(input, cancellationToken);
        ticket.SetAiSummary(reply.Content, DateTime.UtcNow);
        audit.Record("SummarizeTicket", "ServiceTicket", ticket.TicketNumber,
            $"Generated an AI summary of {ticket.TicketNumber} ({ticket.Comments.Count} comments)", AuditSource.AiAssisted);

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TicketInsightsDto> FindRecurringProblemsAsync(int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 7, 365);
        var since = DateTime.UtcNow.AddDays(-days);

        var tickets = await db.ServiceTickets.AsNoTracking()
            .Where(t => t.CreatedAtUtc >= since)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(MaxInsightTickets)
            .Select(t => new
            {
                t.TicketNumber, t.Subject, t.Description, Customer = t.Customer.Name, t.Category, t.Priority, t.Status,
                t.Resolution, t.CreatedAtUtc,
                Product = t.Product == null ? null : t.Product.Code + " " + t.Product.Name,
            })
            .ToListAsync(cancellationToken);

        if (tickets.Count == 0)
        {
            return new TicketInsightsDto($"No tickets were opened in the last {days} days, so there is nothing to analyse.", 0, DateTime.UtcNow);
        }

        var input = new TicketInsightsInput(days, tickets
            .Select(t => new TicketBrief(t.TicketNumber, t.Subject, Truncate(t.Description, 400), t.Customer,
                t.Category.ToString(), t.Priority.ToString(), t.Status.ToString(), t.Product, t.Resolution, t.CreatedAtUtc))
            .ToList());

        var reply = await ai.FindRecurringProblemsAsync(input, cancellationToken);
        return new TicketInsightsDto(reply.Content, tickets.Count, DateTime.UtcNow);
    }

    private async Task EnsureReferencesAsync(SaveTicketRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.Id == request.CustomerId)
            .Select(c => new { c.Status })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);
        if (customer.Status == CustomerStatus.Inactive)
        {
            throw new BusinessRuleException("Tickets can't be raised for an inactive customer.");
        }

        if (request.ProductId is { } productId && !await db.Products.AnyAsync(p => p.Id == productId, cancellationToken))
        {
            throw new NotFoundException("Product", productId);
        }

        if (request.SalesOrderId is { } orderId)
        {
            var orderCustomer = await db.SalesOrders.Where(o => o.Id == orderId).Select(o => (Guid?)o.CustomerId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("SalesOrder", orderId);
            if (orderCustomer != request.CustomerId)
            {
                throw new BusinessRuleException("The linked sales order belongs to a different customer.");
            }
        }
    }

    private async Task<string?> AssigneeNameAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is null) return null;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("The selected assignee does not exist or is inactive.");
        return DisplayName(user.FirstName, user.LastName, user.Email);
    }

    private static void Apply(ServiceTicket ticket, SaveTicketRequest request)
    {
        ticket.Subject = request.Subject.Trim();
        ticket.Description = request.Description.Trim();
        ticket.CustomerId = request.CustomerId;
        ticket.SalesOrderId = request.SalesOrderId;
        ticket.ProductId = request.ProductId;
        ticket.Category = request.Category;
        ticket.Priority = request.Priority;
    }

    private async Task<ServiceTicket> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ServiceTickets.Include(t => t.Comments).FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(ServiceTicket), id);

    private static string DisplayName(string first, string last, string? email)
    {
        var name = $"{first} {last}".Trim();
        return name.Length > 0 ? name : email ?? "Unknown user";
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";

    private static TicketDto ToDto(ServiceTicket t) => new(
        t.Id, t.TicketNumber, t.Subject, t.Description, t.CustomerId, t.Customer.Name, t.SalesOrderId,
        t.SalesOrder?.OrderNumber, t.ProductId, t.Product?.Code, t.Product?.Name, t.Category, t.Priority, t.Status, ServiceTicket.AllowedNext(t.Status),
        t.AssignedToUserId, t.AssignedToName, t.Resolution, t.ResolvedAtUtc, t.ClosedAtUtc, t.AiSummary, t.AiSummaryAtUtc,
        t.Comments.OrderBy(c => c.CreatedAtUtc)
            .Select(c => new TicketCommentDto(c.Id, c.AuthorName, c.Body, c.IsInternal, c.CreatedAtUtc))
            .ToList(),
        t.CreatedAtUtc, t.UpdatedAtUtc);
}
