namespace OpsPilot.Application.Ai;

/// <summary>
/// Read-only query access for the ERP Query agent: one validated SELECT over the <c>ai</c> reporting views the caller's
/// roles allow, executed with a separate read-only database login, a short timeout and a row cap.
/// </summary>
public interface IAiSqlGateway
{
    Task<AiSqlResult> ExecuteAsync(string sql, IReadOnlyList<string> roles, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiViewSchema>> GetSchemaAsync(IReadOnlyList<string> roles, CancellationToken cancellationToken = default);
}

public record AiSqlRequest(string Sql);

public record AiSqlResult(IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows, bool Truncated);

public record AiViewColumn(string Name, string Type);

/// <param name="Accessible">False for views the caller's roles may not read; listed (without columns) so the agent can say so instead of guessing.</param>
public record AiViewSchema(string View, string Description, IReadOnlyList<AiViewColumn> Columns, bool Accessible = true);
