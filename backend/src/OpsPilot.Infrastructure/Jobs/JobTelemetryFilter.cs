using System.Diagnostics;
using Hangfire.Common;
using Hangfire.Server;
using OpsPilot.Application.Common.Observability;

namespace OpsPilot.Infrastructure.Jobs;

/// <summary>Wraps every Hangfire job in a trace span and records its duration and outcome (design doc §42).</summary>
public class JobTelemetryFilter : JobFilterAttribute, IServerFilter
{
    private const string StartedKey = "opspilot.started";
    private const string ActivityKey = "opspilot.activity";

    public void OnPerforming(PerformingContext context)
    {
        context.Items[StartedKey] = Stopwatch.GetTimestamp();
        var activity = OpsPilotTelemetry.ActivitySource.StartActivity($"job {JobName(context)}");
        activity?.SetTag("opspilot.job.id", context.BackgroundJob.Id);
        context.Items[ActivityKey] = activity;
    }

    public void OnPerformed(PerformedContext context)
    {
        var outcome = context.Exception is null ? "ok" : "error";
        if (context.Items.TryGetValue(StartedKey, out var started) && started is long timestamp)
        {
            OpsPilotTelemetry.JobDuration.Record(Stopwatch.GetElapsedTime(timestamp).TotalSeconds,
                OpsPilotTelemetry.Tags(("job", JobName(context)), ("outcome", outcome)));
        }
        if (context.Items.TryGetValue(ActivityKey, out var value) && value is Activity activity)
        {
            if (context.Exception is { } ex) activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.Dispose();
        }
    }

    private static string JobName(PerformContext context) =>
        context.GetJobParameter<string>("RecurringJobId") ?? context.BackgroundJob.Job.Type.Name;
}
