using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Common.Observability;

namespace OpsPilot.Infrastructure.Persistence.Interceptors;

/// <summary>Records every EF Core command's duration (design doc §42) and logs slow ones.</summary>
public class DbCommandMetricsInterceptor(ILogger<DbCommandMetricsInterceptor> logger) : DbCommandInterceptor
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(500);

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Record(command, eventData.Duration, "reader", "ok");
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, "reader", "ok");
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Record(command, eventData.Duration, "non_query", "ok");
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, "non_query", "ok");
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Record(command, eventData.Duration, "scalar", "ok");
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, "scalar", "ok");
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) =>
        Record(command, eventData.Duration, "failed", "error");

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, "failed", "error");
        return Task.CompletedTask;
    }

    private void Record(DbCommand command, TimeSpan duration, string kind, string outcome)
    {
        OpsPilotTelemetry.DbCommandDuration.Record(duration.TotalSeconds, OpsPilotTelemetry.Tags(("kind", kind), ("outcome", outcome)));
        if (duration > SlowThreshold)
        {
            // The SQL text is logged without parameter values, so no data leaks into logs.
            logger.LogWarning("Slow database command ({ElapsedMs:F0} ms, {Kind}): {Sql}", duration.TotalMilliseconds, kind,
                command.CommandText.Length > 300 ? command.CommandText[..300] + "…" : command.CommandText);
        }
    }
}
