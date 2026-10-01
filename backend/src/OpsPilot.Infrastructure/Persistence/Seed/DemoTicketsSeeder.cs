using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Identity;
using OpsPilot.Domain.Sales;
using OpsPilot.Domain.Service;

namespace OpsPilot.Infrastructure.Persistence.Seed;

/// <summary>
/// Demo service tickets over the last ~60 days. Deliberately includes recurring themes (X200 seal leaks, late
/// deliveries, duplicate invoices) so the AI's recurring-problem analysis has something real to find.
/// </summary>
internal sealed class DemoTicketsSeeder(ApplicationDbContext db)
{
    private sealed record Seed(
        int DaysAgo, string Subject, string Description, TicketCategory Category, TicketPriority Priority,
        string? ProductCode, string? Assignee, TicketStatus Status, string? Resolution, (int HoursLater, string Author, string Body, bool Internal)[] Comments);

    public async Task<int> SeedAsync()
    {
        var customers = await db.Customers.Where(c => c.Status != Domain.Customers.CustomerStatus.Inactive)
            .OrderBy(c => c.Code).ToListAsync();
        if (customers.Count == 0) return 0;

        var orders = await db.SalesOrders.AsNoTracking()
            .Where(o => o.Status != SalesOrderStatus.Draft && o.Status != SalesOrderStatus.Cancelled)
            .Select(o => new { o.Id, o.CustomerId, Codes = o.Lines.Select(l => l.Product.Code).ToList(), o.Status })
            .ToListAsync();

        var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Code, p => p.Id);
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Email!, u => u);
        string? Name(string? email) => email is not null && users.TryGetValue(email, out var u) ? $"{u.FirstName} {u.LastName}" : null;
        Guid? Id(string? email) => email is not null && users.TryGetValue(email, out var u) ? u.Id : null;

        const string sales = "sales@opspilot.local", inventory = "inventory@opspilot.local", manager = "manager@opspilot.local";

        Seed[] seeds =
        [
            new(58, "X200 pump leaking at the shaft seal", "Pump installed three weeks ago is dripping at the mechanical seal under load. Customer has a bucket under it.",
                TicketCategory.ProductDefect, TicketPriority.High, "X200", inventory, TicketStatus.Closed, "Replaced the mechanical seal kit under warranty; seal batch B-0412 suspected.",
                [(2, "Ivy Chen", "Asked the customer for photos and the serial number.", false), (20, "Ivy Chen", "Serial X200-2291, seal batch B-0412.", true), (50, "Ivy Chen", "Seal kit shipped, technician visit booked.", false)]),
            new(41, "Second X200 with seal leak", "Another X200 unit leaking from the seal after ~200 operating hours.",
                TicketCategory.ProductDefect, TicketPriority.High, "X200", inventory, TicketStatus.Resolved, "Seal replaced. Same B-0412 batch as TCK earlier.",
                [(3, "Ivy Chen", "Same symptom as the earlier X200 case. Checking seal batch.", true), (30, "Ivy Chen", "Batch B-0412 confirmed again. Replaced seal.", false)]),
            new(9, "X200 seal weeping, customer threatening return", "Third report this quarter of an X200 seal leak. Customer wants a replacement unit or a refund.",
                TicketCategory.ProductDefect, TicketPriority.Urgent, "X200", manager, TicketStatus.InProgress, null,
                [(1, "Sam Patel", "Customer is upset, this is their second pump from us.", false), (5, "Morgan Reyes", "Raising seal batch B-0412 with ABC Industrial Supplies as a supplier quality issue.", true), (26, "Morgan Reyes", "Offered a replacement unit from the next PO.", false)]),
            new(35, "Order arrived 6 days late", "Delivery came almost a week after the required date; the customer's maintenance window was missed.",
                TicketCategory.Delivery, TicketPriority.Medium, null, sales, TicketStatus.Closed, "Apologised and credited the freight charge.",
                [(4, "Sam Patel", "Carrier confirms delay at the regional hub.", true), (30, "Sam Patel", "Freight credit approved.", false)]),
            new(14, "Where is my order? Past the required date", "Customer says the order should have arrived last week and nobody has given them a date.",
                TicketCategory.Delivery, TicketPriority.High, null, sales, TicketStatus.WaitingOnCustomer, null,
                [(2, "Sam Patel", "Stock shortage on one line; partial shipment offered.", false), (6, "Sam Patel", "Waiting for the customer to accept a partial shipment.", true)]),
            new(4, "Late delivery again — third time this quarter", "Customer escalated: repeated late deliveries are affecting their production schedule.",
                TicketCategory.Delivery, TicketPriority.Urgent, null, null, TicketStatus.Open, null,
                [(1, "Sam Patel", "Needs an owner, likely stock-related.", true)]),
            new(27, "Invoice received twice", "Customer got two invoices for the same order and paid neither until it's clarified.",
                TicketCategory.Billing, TicketPriority.Medium, null, manager, TicketStatus.Closed, "Duplicate was a re-sent PDF; confirmed a single invoice is due.",
                [(3, "Morgan Reyes", "Only one invoice in the system; the email was sent twice.", true)]),
            new(6, "Duplicate invoice email", "Same complaint as before: invoice email arrived twice, customer unsure which to pay.",
                TicketCategory.Billing, TicketPriority.Low, null, null, TicketStatus.Open, null, []),
            new(21, "Wiring question for 16A breakers", "Customer's electrician wants the wiring diagram for the 16A circuit breakers in their 3-phase panel.",
                TicketCategory.Installation, TicketPriority.Low, "CB-16A", inventory, TicketStatus.Resolved, "Sent the wiring diagram and installation guide PDF.",
                [(1, "Ivy Chen", "Sent the diagram.", false)]),
            new(11, "Return request: wrong gloves size", "Ordered size L nitrile gloves, received XL.",
                TicketCategory.Returns, TicketPriority.Medium, "GL-NT", sales, TicketStatus.InProgress, null,
                [(2, "Sam Patel", "RMA issued, return label sent.", false)]),
            new(2, "Request for updated price list", "Customer asks for the new price list before their Q4 budget meeting.",
                TicketCategory.General, TicketPriority.Low, null, sales, TicketStatus.Open, null, []),
            new(1, "Pump X200 noisy on start-up", "New X200 makes a grinding noise for the first few seconds after start.",
                TicketCategory.ProductDefect, TicketPriority.Medium, "X200", null, TicketStatus.Open, null, []),
        ];

        var now = DateTime.UtcNow;
        var number = 50001;
        var tickets = new List<ServiceTicket>();
        for (var i = 0; i < seeds.Length; i++)
        {
            var seed = seeds[i];
            var created = now.AddDays(-seed.DaysAgo).AddHours(-(i * 3 % 9));

            // Link to an order with the product (or any order) of a customer, so the ticket has real context.
            var order = orders.FirstOrDefault(o => seed.ProductCode is not null && o.Codes.Contains(seed.ProductCode) && tickets.All(t => t.SalesOrderId != o.Id))
                        ?? (seed.Category == TicketCategory.Delivery ? orders.Skip(i).FirstOrDefault() : null);
            var customerId = order?.CustomerId ?? customers[(i * 5) % customers.Count].Id;

            var ticket = new ServiceTicket
            {
                TicketNumber = $"TCK-{number++}",
                Subject = seed.Subject,
                Description = seed.Description,
                CustomerId = customerId,
                SalesOrderId = order?.Id,
                ProductId = seed.ProductCode is not null && products.TryGetValue(seed.ProductCode, out var productId) ? productId : null,
                Category = seed.Category,
                Priority = seed.Priority,
                CreatedAtUtc = created,
            };
            ticket.Assign(Id(seed.Assignee), Name(seed.Assignee));

            foreach (var (hours, author, body, isInternal) in seed.Comments)
            {
                var authorId = users.Values.FirstOrDefault(u => $"{u.FirstName} {u.LastName}" == author)?.Id;
                ticket.AddComment(body, authorId, author, isInternal, created.AddHours(hours));
            }

            var resolvedAt = created.AddHours(seed.Comments.Length > 0 ? seed.Comments[^1].HoursLater + 2 : 24);
            switch (seed.Status)
            {
                case TicketStatus.InProgress:
                    ticket.TransitionTo(TicketStatus.InProgress, created.AddHours(1));
                    break;
                case TicketStatus.WaitingOnCustomer:
                    ticket.TransitionTo(TicketStatus.InProgress, created.AddHours(1));
                    ticket.TransitionTo(TicketStatus.WaitingOnCustomer, created.AddHours(6));
                    break;
                case TicketStatus.Resolved:
                    ticket.TransitionTo(TicketStatus.InProgress, created.AddHours(1));
                    ticket.TransitionTo(TicketStatus.Resolved, resolvedAt, seed.Resolution);
                    break;
                case TicketStatus.Closed:
                    ticket.TransitionTo(TicketStatus.InProgress, created.AddHours(1));
                    ticket.TransitionTo(TicketStatus.Resolved, resolvedAt, seed.Resolution);
                    ticket.TransitionTo(TicketStatus.Closed, resolvedAt.AddDays(2));
                    break;
            }

            tickets.Add(ticket);
        }

        db.ServiceTickets.AddRange(tickets);
        await db.SaveChangesAsync();
        return tickets.Count;
    }
}
