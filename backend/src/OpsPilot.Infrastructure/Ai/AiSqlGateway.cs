using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Ai;
using OpsPilot.Domain.Common;

namespace OpsPilot.Infrastructure.Ai;

public class AiSqlGateway(IConfiguration configuration, ILogger<AiSqlGateway> logger) : IAiSqlGateway
{
    private const int MaxRows = 200;
    private const int TimeoutSeconds = 5;

    private string ConnectionString =>
        configuration.GetConnectionString("AiReadOnly")
        ?? throw new BusinessRuleException("The AI read-only database connection (ConnectionStrings:AiReadOnly) is not configured.");

    public async Task<AiSqlResult> ExecuteAsync(string sql, IReadOnlyList<string> roles, CancellationToken cancellationToken = default)
    {
        if (AiSqlValidator.Validate(sql, roles) is { } error)
        {
            throw new BusinessRuleException(error);
        }

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection) { CommandTimeout = TimeoutSeconds };
            await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var rows = new List<object?[]>();
            var truncated = false;

            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count == MaxRows)
                {
                    truncated = true;
                    break;
                }

                var values = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    values[i] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
                }
                rows.Add(values);
            }

            logger.LogInformation("AI SQL returned {Rows} rows{Truncated}", rows.Count, truncated ? " (truncated)" : "");
            return new AiSqlResult(columns, rows, truncated);
        }
        catch (SqlException ex)
        {
            // Surfaced to the agent so it can correct its query; never contains data.
            throw new BusinessRuleException($"SQL error: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<AiViewSchema>> GetSchemaAsync(IReadOnlyList<string> roles, CancellationToken cancellationToken = default)
    {
        var columns = await GetColumnsAsync(cancellationToken);

        return AiViews.All.Keys
            .Where(columns.ContainsKey)
            .OrderBy(view => view)
            .Select(view => AiViews.IsAllowed(view, roles)
                ? new AiViewSchema($"ai.{view}", AiViews.All[view].Description, columns[view])
                : new AiViewSchema($"ai.{view}", AiViews.All[view].Description, [], Accessible: false))
            .ToList();
    }

    // View columns only change with a migration, so read them once per process. Catalog queries can be slow when
    // cold, hence the longer timeout here than for user queries.
    private static Dictionary<string, List<AiViewColumn>>? _columns;
    private static readonly SemaphoreSlim ColumnsLock = new(1, 1);

    private async Task<Dictionary<string, List<AiViewColumn>>> GetColumnsAsync(CancellationToken cancellationToken)
    {
        if (_columns is { } cached) return cached;

        await ColumnsLock.WaitAsync(cancellationToken);
        try
        {
            if (_columns is { } again) return again;

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string query = """
                SELECT v.name, c.name, t.name
                FROM sys.views v
                JOIN sys.columns c ON c.object_id = v.object_id
                JOIN sys.types t ON t.user_type_id = c.user_type_id
                WHERE v.schema_id = SCHEMA_ID('ai')
                ORDER BY v.name, c.column_id
                """;

            await using var command = new SqlCommand(query, connection) { CommandTimeout = 30 };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = new Dictionary<string, List<AiViewColumn>>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync(cancellationToken))
            {
                var view = reader.GetString(0);
                if (!columns.TryGetValue(view, out var list))
                {
                    columns[view] = list = [];
                }
                list.Add(new AiViewColumn(reader.GetString(1), reader.GetString(2)));
            }

            logger.LogInformation("Loaded AI view schema: {Views} views", columns.Count);
            return _columns = columns;
        }
        finally
        {
            ColumnsLock.Release();
        }
    }
}
