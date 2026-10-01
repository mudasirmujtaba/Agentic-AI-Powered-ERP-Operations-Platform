using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Ai;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Notifications;
using OpsPilot.Domain.Audit;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Identity;
using OpsPilot.Domain.Insights;
using OpsPilot.Domain.Notifications;

namespace OpsPilot.Application.Jobs;

/// <summary>What a job run did, in one line for the job history.</summary>
public record JobResult(string Summary);

public interface IBackgroundJob
{
    Task<JobResult> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Nightly inventory analysis (design doc §40): "every night at 02:00, the system analyzes inventory, identifies stock
/// risks, generates AI insights, stores the results, and notifies the inventory manager." The analysis itself is the
/// Inventory Intelligence agent's, run as the system principal.
/// </summary>
public class InventoryRiskScanJob(
    IApplicationDbContext db,
    IAiAgentClient ai,
    ISystemPrincipal system,
    NotificationPublisher notifications,
    AuditLogWriter audit,
    ILogger<InventoryRiskScanJob> logger) : IBackgroundJob
{
    public const string Id = "inventory-risk-scan";
    private static readonly string[] Recipients = [Roles.Administrator, Roles.Manager, Roles.InventoryManager];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<JobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var identity = await system.GetAsync(cancellationToken);
        var reply = await ai.ScanInventoryAsync(new AgentJobRequest(
            new AgentUser(identity.UserId, identity.Email, identity.Name, identity.Roles), identity.AccessToken), cancellationToken);

        var report = new InsightReport
        {
            Kind = InsightKinds.InventoryRisk,
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = reply.Content,
            ItemCount = reply.Risks.Count,
            DataJson = JsonSerializer.Serialize(reply.Risks, Json),
            AiGenerated = reply.AiGenerated,
        };
        db.InsightReports.Add(report);

        var critical = reply.Risks.Count(r => r.Risk == "critical");
        if (reply.Risks.Count > 0)
        {
            var top = string.Join(", ", reply.Risks.Take(5).Select(r => r.Product));
            await notifications.NotifyRolesAsync(Recipients,
                $"{reply.Risks.Count} product{(reply.Risks.Count == 1 ? "" : "s")} at stock risk",
                $"Nightly scan of {reply.ProductsAnalysed} products: {critical} critical. Most urgent: {top}.",
                "/automation", critical > 0 ? NotificationSeverity.Critical : NotificationSeverity.Warning, Id, cancellationToken);
        }

        audit.Record("InventoryRiskScan", "InsightReport", report.GeneratedAtUtc.ToString("yyyy-MM-dd"),
            $"Scanned {reply.ProductsAnalysed} products: {reply.Risks.Count} at risk ({critical} critical)", AuditSource.System);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Inventory risk scan: {AtRisk} of {Total} products at risk", reply.Risks.Count, reply.ProductsAnalysed);
        return new JobResult($"{reply.Risks.Count} of {reply.ProductsAnalysed} products at risk ({critical} critical)");
    }
}

/// <summary>Credit and Payment Policy: "Reminders are sent at 7, 14 and 30 days overdue."</summary>
public class OverdueInvoiceReminderJob(
    IApplicationDbContext db,
    NotificationPublisher notifications,
    AuditLogWriter audit) : IBackgroundJob
{
    public const string Id = "overdue-invoice-reminders";
    public static readonly int[] Stages = [7, 14, 30];
    private static readonly string[] Recipients = [Roles.Administrator, Roles.Manager, Roles.FinanceUser];

    public async Task<JobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var overdue = await db.Invoices
            .Include(i => i.Customer)
            .Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDateUtc < today)
            .ToListAsync(cancellationToken);

        var sent = new List<(Invoice Invoice, int Stage, int Days)>();
        foreach (var invoice in overdue)
        {
            var days = invoice.DaysOverdue(now);
            // The highest stage reached; a missed run catches up without sending the skipped stages twice.
            var stage = Stages.Where(s => days >= s).DefaultIfEmpty(0).Max();
            if (stage <= invoice.ReminderStage) continue;

            invoice.MarkReminderSent(stage);
            sent.Add((invoice, stage, days));
            audit.Record("SendPaymentReminder", "Invoice", invoice.InvoiceNumber,
                $"{stage}-day reminder for {invoice.InvoiceNumber} to {invoice.Customer.Name} ({invoice.Balance:N2} outstanding, {days} days overdue)",
                AuditSource.System);
        }

        if (sent.Count > 0)
        {
            var lines = sent.OrderByDescending(s => s.Days).Take(8)
                .Select(s => $"- {s.Invoice.InvoiceNumber} · {s.Invoice.Customer.Name} · ${s.Invoice.Balance:N2} · {s.Days} days ({s.Stage}-day reminder)");
            await notifications.NotifyRolesAsync(Recipients,
                $"{sent.Count} payment reminder{(sent.Count == 1 ? "" : "s")} due",
                string.Join("\n", lines) + (sent.Count > 8 ? $"\n- …and {sent.Count - 8} more" : ""),
                "/finance/invoices?overdueOnly=true", NotificationSeverity.Warning, Id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new JobResult($"{sent.Count} reminders recorded across {overdue.Count} overdue invoices");
    }
}

/// <summary>
/// Credit and Payment Policy: "Finance places a customer on hold when any invoice is more than 60 days overdue, and
/// releases the hold once the account is brought up to date." Only holds this policy placed are released.
/// </summary>
public class CreditHoldReviewJob(
    IApplicationDbContext db,
    NotificationPublisher notifications,
    AuditLogWriter audit) : IBackgroundJob
{
    public const string Id = "credit-hold-review";
    public const int HoldAfterDaysOverdue = 60;
    private static readonly string[] Recipients = [Roles.Administrator, Roles.Manager, Roles.FinanceUser, Roles.SalesUser];

    public async Task<JobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var holdCutoff = today.AddDays(-HoldAfterDaysOverdue);

        var outstanding = await db.Invoices.AsNoTracking()
            .Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDateUtc < today)
            .Select(i => new { i.CustomerId, i.InvoiceNumber, i.DueDateUtc })
            .ToListAsync(cancellationToken);
        var overdueCustomers = outstanding.Select(i => i.CustomerId).ToHashSet();
        var seriouslyOverdue = outstanding.Where(i => i.DueDateUtc < holdCutoff)
            .GroupBy(i => i.CustomerId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.DueDateUtc).First().InvoiceNumber);

        var toHold = await db.Customers
            .Where(c => c.Status == CustomerStatus.Active && seriouslyOverdue.Keys.Contains(c.Id))
            .ToListAsync(cancellationToken);
        var toRelease = await db.Customers
            .Where(c => c.Status == CustomerStatus.OnHold && c.OnPolicyCreditHold && !overdueCustomers.Contains(c.Id))
            .ToListAsync(cancellationToken);

        foreach (var customer in toHold)
        {
            customer.Status = CustomerStatus.OnHold;
            customer.OnPolicyCreditHold = true;
            audit.Record("PlaceCreditHold", "Customer", customer.Code,
                $"Placed {customer.Name} on credit hold: {seriouslyOverdue[customer.Id]} is more than {HoldAfterDaysOverdue} days overdue",
                AuditSource.System);
        }
        foreach (var customer in toRelease)
        {
            customer.Status = CustomerStatus.Active;
            customer.OnPolicyCreditHold = false;
            audit.Record("ReleaseCreditHold", "Customer", customer.Code,
                $"Released {customer.Name} from credit hold: no overdue invoices remain", AuditSource.System);
        }

        if (toHold.Count + toRelease.Count > 0)
        {
            var lines = toHold.Select(c => $"- **On hold:** {c.Name} ({seriouslyOverdue[c.Id]} over {HoldAfterDaysOverdue} days overdue)")
                .Concat(toRelease.Select(c => $"- **Released:** {c.Name} (account up to date)"));
            await notifications.NotifyRolesAsync(Recipients, "Credit holds updated", string.Join("\n", lines),
                "/customers", toHold.Count > 0 ? NotificationSeverity.Warning : NotificationSeverity.Info, Id, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new JobResult($"{toHold.Count} placed on hold, {toRelease.Count} released");
    }
}
