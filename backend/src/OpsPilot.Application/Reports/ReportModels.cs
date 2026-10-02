using IdentityRoles = OpsPilot.Domain.Identity.Roles;

namespace OpsPilot.Application.Reports;

/// <summary>How a value should be displayed; the UI formats, the API stays numeric.</summary>
public enum ValueFormat
{
    Number,
    Currency,
    Percent,
    Days,
    Text,
    Date
}

public record ReportKpi(string Label, decimal? Value, ValueFormat Format, string? Hint = null);

public record SeriesPoint(string Label, decimal Value);

public record ReportSeries(string Title, ValueFormat Format, IReadOnlyList<SeriesPoint> Points);

public record ReportColumn(string Key, string Label, ValueFormat Format = ValueFormat.Text);

public record ReportTable(string Title, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    string? EmptyText = null);

/// <summary>
/// One report (design doc §48) in a shape every report shares: headline figures, an optional monthly trend and
/// detail tables. The same tables back the CSV export.
/// </summary>
public record ReportDto(
    string Key,
    string Title,
    DateOnly From,
    DateOnly To,
    DateTime GeneratedAtUtc,
    IReadOnlyList<ReportKpi> Kpis,
    ReportSeries? Trend,
    IReadOnlyList<ReportTable> Tables);

public class ReportQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public static class ReportKeys
{
    public const string Sales = "sales";
    public const string Inventory = "inventory";
    public const string Procurement = "procurement";
    public const string Finance = "finance";
    public const string Ai = "ai";

    public static readonly IReadOnlyList<string> All = [Sales, Inventory, Procurement, Finance, Ai];

    /// <summary>Who may read each report: leadership sees everything, each function sees its own domain.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Readers = new Dictionary<string, string[]>
    {
        [Sales] = [IdentityRoles.Administrator, IdentityRoles.Manager, IdentityRoles.SalesUser, IdentityRoles.FinanceUser],
        [Inventory] = [IdentityRoles.Administrator, IdentityRoles.Manager, IdentityRoles.InventoryManager, IdentityRoles.ProcurementUser],
        [Procurement] = [IdentityRoles.Administrator, IdentityRoles.Manager, IdentityRoles.ProcurementUser, IdentityRoles.FinanceUser],
        [Finance] = [IdentityRoles.Administrator, IdentityRoles.Manager, IdentityRoles.FinanceUser],
        [Ai] = [IdentityRoles.Administrator, IdentityRoles.Manager],
    };

    public static bool CanRead(string key, IEnumerable<string> userRoles) =>
        Readers.TryGetValue(key, out var allowed) && userRoles.Any(allowed.Contains);
}
