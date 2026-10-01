using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace OpsPilot.Infrastructure.Ai;

/// <summary>
/// Parses AI-generated SQL with the real T-SQL grammar and accepts it only if it is a single read-only SELECT that
/// references nothing but permitted <c>ai.*</c> views (or its own CTEs). Defense in depth: the query also runs under a
/// login that can read only the <c>ai</c> schema.
/// </summary>
public static class AiSqlValidator
{
    public static string? Validate(string sql, IReadOnlyList<string> userRoles)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return "The query is empty.";
        }

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out var errors);
        if (errors.Count > 0)
        {
            return $"SQL syntax error: {errors[0].Message} (line {errors[0].Line}).";
        }

        if (fragment is not TSqlScript { Batches.Count: 1 } script || script.Batches[0].Statements.Count != 1)
        {
            return "Exactly one SELECT statement is allowed.";
        }

        if (script.Batches[0].Statements[0] is not SelectStatement select)
        {
            return "Only SELECT statements are allowed.";
        }

        if (select.Into is not null)
        {
            return "SELECT ... INTO is not allowed.";
        }

        var visitor = new ReferenceVisitor();
        select.Accept(visitor);

        if (visitor.Forbidden is { } forbidden)
        {
            return forbidden;
        }

        foreach (var table in visitor.Tables)
        {
            var schema = table.SchemaObject.SchemaIdentifier?.Value;
            var name = table.SchemaObject.BaseIdentifier.Value;

            if (schema is null && visitor.CteNames.Contains(name))
            {
                continue;
            }

            if (table.SchemaObject.ServerIdentifier is not null || table.SchemaObject.DatabaseIdentifier is not null)
            {
                return $"Cross-database references are not allowed ({table.SchemaObject.BaseIdentifier.Value}).";
            }

            if (!string.Equals(schema, "ai", StringComparison.OrdinalIgnoreCase))
            {
                return $"Only views in the ai schema can be queried; '{(schema is null ? name : $"{schema}.{name}")}' is not one of them.";
            }

            if (!AiViews.All.ContainsKey(name))
            {
                return $"Unknown view ai.{name}. Available views: {string.Join(", ", AiViews.AllowedFor(userRoles).Select(v => $"ai.{v}"))}.";
            }

            if (!AiViews.IsAllowed(name, userRoles))
            {
                return $"Your role does not have access to ai.{name}.";
            }
        }

        return null;
    }

    private sealed class ReferenceVisitor : TSqlFragmentVisitor
    {
        public List<NamedTableReference> Tables { get; } = [];
        public HashSet<string> CteNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? Forbidden { get; private set; }

        public override void ExplicitVisit(NamedTableReference node)
        {
            Tables.Add(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommonTableExpression node)
        {
            CteNames.Add(node.ExpressionName.Value);
            base.ExplicitVisit(node);
        }

        public override void Visit(OpenRowsetTableReference node) => Forbidden ??= "OPENROWSET is not allowed.";
        public override void Visit(OpenQueryTableReference node) => Forbidden ??= "OPENQUERY is not allowed.";
        public override void Visit(OpenXmlTableReference node) => Forbidden ??= "OPENXML is not allowed.";
        public override void Visit(BulkOpenRowset node) => Forbidden ??= "Bulk reads are not allowed.";
        public override void Visit(SchemaObjectFunctionTableReference node) => Forbidden ??= "Table-valued functions are not allowed.";
        public override void Visit(ExecuteStatement node) => Forbidden ??= "EXECUTE is not allowed.";
    }
}
