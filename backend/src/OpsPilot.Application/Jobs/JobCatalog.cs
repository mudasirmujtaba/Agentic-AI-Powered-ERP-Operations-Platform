using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Ai;
using OpsPilot.Application.Common.Interfaces;

namespace OpsPilot.Application.Jobs;

public record JobDefinition(string Id, string Name, string Description, string Cron, string Schedule, Type JobType);

/// <summary>The scheduled jobs and when they run (local server time). Cron is standard 5-field.</summary>
public static class JobCatalog
{
    public static readonly IReadOnlyList<JobDefinition> All =
    [
        new(InventoryRiskScanJob.Id, "Inventory risk scan",
            "Analyses stock, sales velocity, open purchase orders and lead times; stores an AI-written insight report and notifies inventory managers.",
            "0 2 * * *", "Daily at 02:00", typeof(InventoryRiskScanJob)),
        new(CreditHoldReviewJob.Id, "Credit hold review",
            "Places customers on hold when an invoice is over 60 days overdue, and releases policy holds once accounts are current.",
            "30 6 * * *", "Daily at 06:30", typeof(CreditHoldReviewJob)),
        new(OverdueInvoiceReminderJob.Id, "Overdue invoice reminders",
            "Records 7-, 14- and 30-day payment reminders and sends finance a digest.",
            "0 7 * * *", "Daily at 07:00", typeof(OverdueInvoiceReminderJob)),
    ];

    public static JobDefinition? Find(string id) => All.FirstOrDefault(j => j.Id == id);
}

public record JobStatusDto(
    string Id,
    string Name,
    string Description,
    string Schedule,
    DateTime? NextRunUtc,
    DateTime? LastRunUtc,
    string? LastState,
    string? LastResult);

/// <summary>Scheduler-facing operations (implemented over Hangfire in Infrastructure).</summary>
public interface IJobScheduler
{
    Task<IReadOnlyList<JobStatusDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Queues the job to run now; returns the scheduler's run id.</summary>
    string Trigger(string jobId);
}

public record InsightReportDto(
    Guid Id,
    string Kind,
    DateTime GeneratedAtUtc,
    string Summary,
    bool AiGenerated,
    int ItemCount,
    IReadOnlyList<InventoryRiskItem> Items);

public interface IInsightService
{
    Task<InsightReportDto?> GetLatestAsync(string kind, CancellationToken cancellationToken = default);
}

public class InsightService(IApplicationDbContext db) : IInsightService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<InsightReportDto?> GetLatestAsync(string kind, CancellationToken cancellationToken = default)
    {
        var report = await db.InsightReports.AsNoTracking()
            .Where(r => r.Kind == kind)
            .OrderByDescending(r => r.GeneratedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return report is null
            ? null
            : new InsightReportDto(report.Id, report.Kind, report.GeneratedAtUtc, report.Summary, report.AiGenerated, report.ItemCount,
                JsonSerializer.Deserialize<List<InventoryRiskItem>>(report.DataJson, Json) ?? []);
    }
}
