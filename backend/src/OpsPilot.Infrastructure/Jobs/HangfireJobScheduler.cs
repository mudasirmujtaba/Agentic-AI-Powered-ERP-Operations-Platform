using Hangfire;
using Hangfire.Storage;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Jobs;

namespace OpsPilot.Infrastructure.Jobs;

/// <summary>Registers the <see cref="JobCatalog"/> as Hangfire recurring jobs and reports their state.</summary>
public class HangfireJobScheduler(JobStorage storage, IRecurringJobManager recurringJobs) : IJobScheduler
{
    public static void RegisterRecurringJobs(IRecurringJobManager manager, TimeZoneInfo timeZone)
    {
        var options = new RecurringJobOptions { TimeZone = timeZone };
        foreach (var job in JobCatalog.All)
        {
            // The generic overloads need the concrete type at compile time, so dispatch on the catalog entry.
            switch (job.Id)
            {
                case InventoryRiskScanJob.Id:
                    manager.AddOrUpdate<InventoryRiskScanJob>(job.Id, j => j.RunAsync(CancellationToken.None), job.Cron, options);
                    break;
                case CreditHoldReviewJob.Id:
                    manager.AddOrUpdate<CreditHoldReviewJob>(job.Id, j => j.RunAsync(CancellationToken.None), job.Cron, options);
                    break;
                case OverdueInvoiceReminderJob.Id:
                    manager.AddOrUpdate<OverdueInvoiceReminderJob>(job.Id, j => j.RunAsync(CancellationToken.None), job.Cron, options);
                    break;
                default:
                    throw new InvalidOperationException($"Job {job.Id} has no Hangfire registration.");
            }
        }
    }

    public Task<IReadOnlyList<JobStatusDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = storage.GetConnection();
        var recurring = connection.GetRecurringJobs().ToDictionary(r => r.Id);
        var monitoring = storage.GetMonitoringApi();

        IReadOnlyList<JobStatusDto> result = JobCatalog.All.Select(job =>
        {
            recurring.TryGetValue(job.Id, out var state);
            return new JobStatusDto(job.Id, job.Name, job.Description, job.Schedule,
                state?.NextExecution, state?.LastExecution, state?.LastJobState, LastResult(monitoring, state?.LastJobId));
        }).ToList();
        return Task.FromResult(result);
    }

    public string Trigger(string jobId)
    {
        if (JobCatalog.Find(jobId) is null) throw new NotFoundException("Job", jobId);
        if (recurringJobs is IRecurringJobManagerV2 v2)
        {
            return v2.TriggerJob(jobId) ?? throw new InvalidOperationException($"Job {jobId} is not registered with the scheduler.");
        }
        recurringJobs.Trigger(jobId);
        return jobId;
    }

    /// <summary>The <see cref="JobResult.Summary"/> of the last successful run, or the failure reason.</summary>
    private static string? LastResult(IMonitoringApi monitoring, string? jobId)
    {
        if (jobId is null) return null;
        var details = monitoring.JobDetails(jobId);
        var latest = details?.History.FirstOrDefault();
        if (latest is null) return null;

        if (latest.Data.TryGetValue("Result", out var json) && !string.IsNullOrEmpty(json))
        {
            try
            {
                // Hangfire serialises results with its own settings, so match the property name case-insensitively.
                return System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateObject()
                    .FirstOrDefault(p => p.Name.Equals(nameof(JobResult.Summary), StringComparison.OrdinalIgnoreCase))
                    .Value.GetString();
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return null;
            }
        }
        return latest.Reason ?? (latest.Data.TryGetValue("ExceptionMessage", out var message) ? message : null);
    }
}
