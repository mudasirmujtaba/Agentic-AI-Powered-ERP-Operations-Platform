using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Ai;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Purchasing;
using OpsPilot.Domain.Sales;

namespace OpsPilot.Application.Reports;

public interface IReportService
{
    Task<ReportDto> GetAsync(string key, ReportQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// The five report domains of design doc §48. Each report projects only the columns it needs for the period and
/// aggregates in memory, which keeps the queries portable (SQLite in tests) at this data scale.
/// </summary>
public class ReportService(IApplicationDbContext db) : IReportService
{
    private const int DefaultDays = 90;
    private const int MaxDays = 731;

    public Task<ReportDto> GetAsync(string key, ReportQuery query, CancellationToken cancellationToken = default)
    {
        var period = Period.Of(query);
        return key switch
        {
            ReportKeys.Sales => SalesAsync(period, cancellationToken),
            ReportKeys.Inventory => InventoryAsync(period, cancellationToken),
            ReportKeys.Procurement => ProcurementAsync(period, cancellationToken),
            ReportKeys.Finance => FinanceAsync(period, cancellationToken),
            ReportKeys.Ai => AiAsync(period, cancellationToken),
            _ => throw new NotFoundException("Report", key),
        };
    }

    private async Task<ReportDto> SalesAsync(Period period, CancellationToken ct)
    {
        var orders = await db.SalesOrders.AsNoTracking()
            .Where(o => o.OrderDateUtc >= period.StartUtc && o.OrderDateUtc < period.EndUtc
                        && o.Status != SalesOrderStatus.Draft && o.Status != SalesOrderStatus.Cancelled)
            .Select(o => new { o.OrderDateUtc, o.TotalAmount, o.Status, o.CustomerId, Customer = o.Customer.Name })
            .ToListAsync(ct);
        var lines = await db.SalesOrders.AsNoTracking()
            .Where(o => o.OrderDateUtc >= period.StartUtc && o.OrderDateUtc < period.EndUtc
                        && o.Status != SalesOrderStatus.Draft && o.Status != SalesOrderStatus.Cancelled)
            .SelectMany(o => o.Lines.Select(l => new { l.Product.Code, l.Product.Name, l.Quantity, l.UnitPrice, l.Product.Cost }))
            .ToListAsync(ct);

        var revenue = orders.Sum(o => o.TotalAmount);
        var cost = lines.Sum(l => l.Quantity * l.Cost);

        return new ReportDto(ReportKeys.Sales, "Sales", period.From, period.To, DateTime.UtcNow,
            [
                new("Revenue", revenue, ValueFormat.Currency, "Confirmed, shipped and delivered orders by order date"),
                new("Orders", orders.Count, ValueFormat.Number),
                new("Average order value", orders.Count == 0 ? null : Math.Round(revenue / orders.Count, 2), ValueFormat.Currency),
                new("Active customers", orders.Select(o => o.CustomerId).Distinct().Count(), ValueFormat.Number),
                new("Gross margin", revenue == 0 ? null : Math.Round((revenue - cost) / revenue, 4), ValueFormat.Percent, "Based on current product cost"),
            ],
            new ReportSeries("Revenue by month", ValueFormat.Currency,
                period.Months().Select(m => new SeriesPoint(m.Label, orders.Where(o => m.Contains(o.OrderDateUtc)).Sum(o => o.TotalAmount))).ToList()),
            [
                Table("Revenue by customer",
                    [new("customer", "Customer"), new("orders", "Orders", ValueFormat.Number), new("revenue", "Revenue", ValueFormat.Currency),
                     new("share", "Share", ValueFormat.Percent), new("aov", "Avg. order", ValueFormat.Currency)],
                    orders.GroupBy(o => o.Customer).OrderByDescending(g => g.Sum(o => o.TotalAmount)).Take(15).Select(g => Row(
                        ("customer", g.Key), ("orders", g.Count()), ("revenue", g.Sum(o => o.TotalAmount)),
                        ("share", revenue == 0 ? 0 : Math.Round(g.Sum(o => o.TotalAmount) / revenue, 4)),
                        ("aov", Math.Round(g.Sum(o => o.TotalAmount) / g.Count(), 2))))),
                Table("Top products",
                    [new("product", "Product"), new("name", "Name"), new("units", "Units", ValueFormat.Number), new("revenue", "Revenue", ValueFormat.Currency)],
                    lines.GroupBy(l => (l.Code, l.Name)).OrderByDescending(g => g.Sum(l => l.Quantity * l.UnitPrice)).Take(10).Select(g => Row(
                        ("product", g.Key.Code), ("name", g.Key.Name), ("units", g.Sum(l => l.Quantity)), ("revenue", g.Sum(l => l.Quantity * l.UnitPrice))))),
                Table("Orders by status",
                    [new("status", "Status"), new("orders", "Orders", ValueFormat.Number), new("value", "Value", ValueFormat.Currency)],
                    orders.GroupBy(o => o.Status).OrderBy(g => g.Key).Select(g => Row(
                        ("status", g.Key.ToString()), ("orders", g.Count()), ("value", g.Sum(o => o.TotalAmount))))),
            ]);
    }

    private async Task<ReportDto> InventoryAsync(Period period, CancellationToken ct)
    {
        var stock = await db.InventoryItems.AsNoTracking()
            .Select(i => new
            {
                i.Product.Code, i.Product.Name, i.Product.Cost, i.Product.ReorderPoint, i.Product.SafetyStock,
                Warehouse = i.Warehouse.Code, i.QuantityOnHand, i.QuantityReserved,
            })
            .ToListAsync(ct);
        var movements = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.OccurredAtUtc >= period.StartUtc && t.OccurredAtUtc < period.EndUtc)
            .Select(t => new { t.Type, t.Quantity, t.OccurredAtUtc, t.Product.Code, t.Product.Name, t.Product.Cost })
            .ToListAsync(ct);

        var byProduct = stock.GroupBy(s => (s.Code, s.Name, s.Cost, s.ReorderPoint, s.SafetyStock))
            .Select(g => new
            {
                g.Key.Code, g.Key.Name, g.Key.ReorderPoint, g.Key.SafetyStock,
                OnHand = g.Sum(s => s.QuantityOnHand), Available = g.Sum(s => s.QuantityOnHand - s.QuantityReserved),
                Value = g.Sum(s => s.QuantityOnHand * s.Cost),
            }).ToList();
        var value = byProduct.Sum(p => p.Value);
        // Turnover = cost of goods sold in the period / current inventory value, annualised.
        var cogs = movements.Where(m => m.Type == InventoryTransactionType.Sale).Sum(m => -m.Quantity * m.Cost);
        var turnover = value == 0 ? (decimal?)null : Math.Round(cogs / value * 365m / period.Days, 2);

        return new ReportDto(ReportKeys.Inventory, "Inventory", period.From, period.To, DateTime.UtcNow,
            [
                new("Stock value", value, ValueFormat.Currency, "Units on hand at current cost"),
                new("Units on hand", byProduct.Sum(p => p.OnHand), ValueFormat.Number),
                new("Low-stock products", byProduct.Count(p => p.Available <= p.ReorderPoint), ValueFormat.Number, "Available at or below reorder point"),
                new("Out of stock", byProduct.Count(p => p.Available <= 0), ValueFormat.Number),
                new("Inventory turnover", turnover, ValueFormat.Number, "Annualised: cost of goods sold / stock value"),
            ],
            new ReportSeries("Units sold by month", ValueFormat.Number,
                period.Months().Select(m => new SeriesPoint(m.Label,
                    movements.Where(x => x.Type == InventoryTransactionType.Sale && m.Contains(x.OccurredAtUtc)).Sum(x => -x.Quantity))).ToList()),
            [
                Table("Low-stock items",
                    [new("product", "Product"), new("name", "Name"), new("available", "Available", ValueFormat.Number),
                     new("reorder", "Reorder point", ValueFormat.Number), new("safety", "Safety stock", ValueFormat.Number), new("status", "Status")],
                    byProduct.Where(p => p.Available <= p.ReorderPoint).OrderBy(p => p.Available - p.SafetyStock).Select(p => Row(
                        ("product", p.Code), ("name", p.Name), ("available", p.Available), ("reorder", p.ReorderPoint), ("safety", p.SafetyStock),
                        ("status", p.Available <= 0 ? "Out of stock" : p.Available < p.SafetyStock ? "Below safety stock" : "Reorder"))),
                    "Every product is above its reorder point."),
                Table("Stock movement by type",
                    [new("type", "Movement"), new("transactions", "Transactions", ValueFormat.Number),
                     new("unitsIn", "Units in", ValueFormat.Number), new("unitsOut", "Units out", ValueFormat.Number)],
                    movements.GroupBy(m => m.Type).OrderBy(g => g.Key).Select(g => Row(
                        ("type", g.Key.ToString()), ("transactions", g.Count()),
                        ("unitsIn", g.Where(m => m.Quantity > 0).Sum(m => m.Quantity)), ("unitsOut", g.Where(m => m.Quantity < 0).Sum(m => -m.Quantity))))),
                Table("Stock value by product",
                    [new("product", "Product"), new("name", "Name"), new("onHand", "On hand", ValueFormat.Number), new("value", "Value", ValueFormat.Currency)],
                    byProduct.OrderByDescending(p => p.Value).Take(15).Select(p => Row(
                        ("product", p.Code), ("name", p.Name), ("onHand", p.OnHand), ("value", p.Value)))),
            ]);
    }

    private async Task<ReportDto> ProcurementAsync(Period period, CancellationToken ct)
    {
        var orders = await db.PurchaseOrders.AsNoTracking()
            .Where(p => p.OrderDateUtc >= period.StartUtc && p.OrderDateUtc < period.EndUtc && p.Status != PurchaseOrderStatus.Cancelled)
            .Select(p => new
            {
                p.PoNumber, Supplier = p.Supplier.Name, p.Supplier.AverageLeadTimeDays, p.Status, p.OrderDateUtc,
                p.ExpectedDeliveryDateUtc, p.CompletedAtUtc, p.TotalAmount,
            })
            .ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        var placed = orders.Where(o => o.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.PendingApproval)).ToList();
        var completed = placed.Where(o => o.CompletedAtUtc is not null && o.ExpectedDeliveryDateUtc is not null).ToList();
        bool OnTime(DateTime? done, DateTime? expected) => done!.Value.Date <= expected!.Value.Date;
        var late = placed.Where(o => o.CompletedAtUtc is null && o.ExpectedDeliveryDateUtc < today).ToList();

        return new ReportDto(ReportKeys.Procurement, "Procurement", period.From, period.To, DateTime.UtcNow,
            [
                new("Purchase volume", orders.Sum(o => o.TotalAmount), ValueFormat.Currency, "All non-cancelled purchase orders by order date"),
                new("Purchase orders", orders.Count, ValueFormat.Number),
                new("Average PO value", orders.Count == 0 ? null : Math.Round(orders.Average(o => o.TotalAmount), 2), ValueFormat.Currency),
                new("On-time delivery", completed.Count == 0 ? null : Math.Round((decimal)completed.Count(o => OnTime(o.CompletedAtUtc, o.ExpectedDeliveryDateUtc)) / completed.Count, 4),
                    ValueFormat.Percent, "Completed orders received by the expected date"),
                new("Overdue deliveries", late.Count, ValueFormat.Number, "Open orders past their expected date"),
            ],
            new ReportSeries("Purchase volume by month", ValueFormat.Currency,
                period.Months().Select(m => new SeriesPoint(m.Label, orders.Where(o => m.Contains(o.OrderDateUtc)).Sum(o => o.TotalAmount))).ToList()),
            [
                Table("Supplier performance",
                    [new("supplier", "Supplier"), new("orders", "Orders", ValueFormat.Number), new("spend", "Spend", ValueFormat.Currency),
                     new("onTime", "On time", ValueFormat.Percent), new("avgDelay", "Avg. delay", ValueFormat.Days), new("leadTime", "Quoted lead time", ValueFormat.Days)],
                    orders.GroupBy(o => (o.Supplier, o.AverageLeadTimeDays)).OrderByDescending(g => g.Sum(o => o.TotalAmount)).Select(g =>
                    {
                        var done = g.Where(o => o.CompletedAtUtc is not null && o.ExpectedDeliveryDateUtc is not null).ToList();
                        return Row(("supplier", g.Key.Supplier), ("orders", g.Count()), ("spend", g.Sum(o => o.TotalAmount)),
                            ("onTime", done.Count == 0 ? null : Math.Round((decimal)done.Count(o => OnTime(o.CompletedAtUtc, o.ExpectedDeliveryDateUtc)) / done.Count, 4)),
                            ("avgDelay", done.Count == 0 ? null : Math.Round(done.Average(o => Math.Max(0, (o.CompletedAtUtc!.Value.Date - o.ExpectedDeliveryDateUtc!.Value.Date).Days)), 1)),
                            ("leadTime", g.Key.AverageLeadTimeDays));
                    })),
                Table("Delivery delays",
                    [new("po", "Purchase order"), new("supplier", "Supplier"), new("expected", "Expected", ValueFormat.Date),
                     new("daysLate", "Days late", ValueFormat.Days), new("value", "Value", ValueFormat.Currency), new("status", "Status")],
                    late.OrderBy(o => o.ExpectedDeliveryDateUtc).Select(o => Row(
                        ("po", o.PoNumber), ("supplier", o.Supplier), ("expected", o.ExpectedDeliveryDateUtc),
                        ("daysLate", (today - o.ExpectedDeliveryDateUtc!.Value.Date).Days), ("value", o.TotalAmount), ("status", o.Status.ToString()))),
                    "No open purchase orders are past their expected date."),
            ]);
    }

    private async Task<ReportDto> FinanceAsync(Period period, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var open = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            .Select(i => new { i.InvoiceNumber, Customer = i.Customer.Name, i.DueDateUtc, i.TotalAmount, i.AmountPaid })
            .ToListAsync(ct);
        var issued = await db.Invoices.AsNoTracking()
            .Where(i => i.IssueDateUtc >= period.StartUtc && i.IssueDateUtc < period.EndUtc && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.IssueDateUtc, i.TotalAmount })
            .ToListAsync(ct);
        var payments = await db.Invoices.AsNoTracking()
            .SelectMany(i => i.Payments)
            .Where(p => p.PaidAtUtc >= period.StartUtc && p.PaidAtUtc < period.EndUtc)
            .Select(p => new { p.PaidAtUtc, p.Amount, p.Method })
            .ToListAsync(ct);

        int DaysOverdue(DateTime? due) => due is { } d && d.Date < today ? (today - d.Date).Days : 0;
        var outstanding = open.Sum(i => i.TotalAmount - i.AmountPaid);
        var overdue = open.Where(i => DaysOverdue(i.DueDateUtc) > 0).ToList();
        var invoiced = issued.Sum(i => i.TotalAmount);
        // Days sales outstanding: receivables relative to the period's average daily billing.
        var dso = invoiced == 0 ? (decimal?)null : Math.Round(outstanding / (invoiced / period.Days), 1);

        (string Label, Func<int, bool> Match)[] buckets =
        [
            ("Current", d => d == 0), ("1-30 days", d => d is >= 1 and <= 30), ("31-60 days", d => d is >= 31 and <= 60),
            ("61-90 days", d => d is >= 61 and <= 90), ("Over 90 days", d => d > 90),
        ];

        return new ReportDto(ReportKeys.Finance, "Finance", period.From, period.To, DateTime.UtcNow,
            [
                new("Outstanding receivables", outstanding, ValueFormat.Currency, "All issued, unpaid balances (as of today)"),
                new("Overdue", overdue.Sum(i => i.TotalAmount - i.AmountPaid), ValueFormat.Currency, $"{overdue.Count} invoices past due"),
                new("Invoiced", invoiced, ValueFormat.Currency, "Invoices issued in the period"),
                new("Collected", payments.Sum(p => p.Amount), ValueFormat.Currency, "Payments received in the period"),
                new("Days sales outstanding", dso, ValueFormat.Days),
            ],
            new ReportSeries("Payments received by month", ValueFormat.Currency,
                period.Months().Select(m => new SeriesPoint(m.Label, payments.Where(p => m.Contains(p.PaidAtUtc)).Sum(p => p.Amount))).ToList()),
            [
                Table("Receivables aging",
                    [new("bucket", "Age"), new("invoices", "Invoices", ValueFormat.Number), new("balance", "Balance", ValueFormat.Currency),
                     new("share", "Share", ValueFormat.Percent)],
                    buckets.Select(b =>
                    {
                        var inBucket = open.Where(i => b.Match(DaysOverdue(i.DueDateUtc))).ToList();
                        var balance = inBucket.Sum(i => i.TotalAmount - i.AmountPaid);
                        return Row(("bucket", b.Label), ("invoices", inBucket.Count), ("balance", balance),
                            ("share", outstanding == 0 ? 0 : Math.Round(balance / outstanding, 4)));
                    })),
                Table("Overdue invoices",
                    [new("invoice", "Invoice"), new("customer", "Customer"), new("due", "Due", ValueFormat.Date),
                     new("daysOverdue", "Days overdue", ValueFormat.Days), new("balance", "Balance", ValueFormat.Currency)],
                    overdue.OrderByDescending(i => DaysOverdue(i.DueDateUtc)).Select(i => Row(
                        ("invoice", i.InvoiceNumber), ("customer", i.Customer), ("due", i.DueDateUtc),
                        ("daysOverdue", DaysOverdue(i.DueDateUtc)), ("balance", i.TotalAmount - i.AmountPaid))),
                    "No invoices are overdue."),
                Table("Payments by method",
                    [new("method", "Method"), new("payments", "Payments", ValueFormat.Number), new("amount", "Amount", ValueFormat.Currency)],
                    payments.GroupBy(p => p.Method).OrderByDescending(g => g.Sum(p => p.Amount)).Select(g => Row(
                        ("method", g.Key.ToString()), ("payments", g.Count()), ("amount", g.Sum(p => p.Amount))))),
            ]);
    }

    private async Task<ReportDto> AiAsync(Period period, CancellationToken ct)
    {
        var messages = await db.AiConversations.AsNoTracking()
            .SelectMany(c => c.Messages)
            .Where(m => m.CreatedAtUtc >= period.StartUtc && m.CreatedAtUtc < period.EndUtc && m.Role == AiMessageRole.Assistant)
            .Select(m => new { m.CreatedAtUtc, m.MetadataJson })
            .ToListAsync(ct);
        var actions = await db.AiActions.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= period.StartUtc && a.CreatedAtUtc < period.EndUtc)
            .Select(a => new { a.Agent, a.ActionType, a.Status })
            .ToListAsync(ct);

        var runs = messages.Select(m => AgentRun.Parse(m.CreatedAtUtc, m.MetadataJson)).ToList();
        var decided = actions.Count(a => a.Status != AiActionStatus.Pending);
        var approved = actions.Count(a => a.Status is AiActionStatus.Approved or AiActionStatus.Executed or AiActionStatus.Failed);
        var tools = runs.SelectMany(r => r.Tools).ToList();

        return new ReportDto(ReportKeys.Ai, "AI operations", period.From, period.To, DateTime.UtcNow,
            [
                new("Agent executions", runs.Count, ValueFormat.Number, "Copilot answers produced"),
                new("Recommendations", actions.Count, ValueFormat.Number, "Actions proposed for approval"),
                new("Approval rate", decided == 0 ? null : Math.Round((decimal)approved / decided, 4), ValueFormat.Percent, "Approved of decided proposals"),
                new("AI errors", runs.Count(r => r.Intent == "error") + tools.Count(t => !t.Ok), ValueFormat.Number, "Failed runs and failed tool calls"),
                new("Tokens used", runs.Sum(r => r.Tokens), ValueFormat.Number),
            ],
            new ReportSeries("Agent executions by month", ValueFormat.Number,
                period.Months().Select(m => new SeriesPoint(m.Label, runs.Count(r => m.Contains(r.At)))).ToList()),
            [
                Table("Executions by agent",
                    [new("intent", "Agent / intent"), new("runs", "Runs", ValueFormat.Number), new("avgSeconds", "Avg. time (s)", ValueFormat.Number),
                     new("tokens", "Tokens", ValueFormat.Number)],
                    runs.GroupBy(r => r.Intent).OrderByDescending(g => g.Count()).Select(g => Row(
                        ("intent", Humanize(g.Key)), ("runs", g.Count()),
                        ("avgSeconds", Math.Round(g.Average(r => r.Seconds), 1)), ("tokens", g.Sum(r => r.Tokens))))),
                Table("Recommendations and decisions",
                    [new("status", "Status"), new("actions", "Actions", ValueFormat.Number)],
                    actions.GroupBy(a => a.Status).OrderBy(g => g.Key).Select(g => Row(("status", g.Key.ToString()), ("actions", g.Count()))),
                    "No AI actions were proposed in this period."),
                Table("Tool usage",
                    [new("tool", "Tool"), new("calls", "Calls", ValueFormat.Number), new("failures", "Failures", ValueFormat.Number),
                     new("avgMs", "Avg. time (ms)", ValueFormat.Number)],
                    tools.GroupBy(t => t.Name).OrderByDescending(g => g.Count()).Select(g => Row(
                        ("tool", g.Key), ("calls", g.Count()), ("failures", g.Count(t => !t.Ok)), ("avgMs", Math.Round(g.Average(t => t.Ms), 0))))),
            ]);
    }

    private static string Humanize(string intent) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(intent.Replace('_', ' '));

    private static ReportTable Table(string title, IReadOnlyList<ReportColumn> columns,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows, string? emptyText = null) =>
        new(title, columns, rows.ToList(), emptyText);

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] cells) =>
        cells.ToDictionary(c => c.Key, c => c.Value);

    /// <summary>One Copilot answer, read from the metadata stored with the message.</summary>
    private sealed record AgentRun(DateTime At, string Intent, double Seconds, long Tokens, IReadOnlyList<ToolRun> Tools)
    {
        public static AgentRun Parse(DateTime at, string? json)
        {
            if (json is null) return new AgentRun(at, "unknown", 0, 0, []);
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var intent = root.TryGetProperty("intent", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString()! : "unknown";
                var meta = root.TryGetProperty("metadata", out var m) && m.ValueKind == JsonValueKind.Object ? m : root;
                var seconds = meta.TryGetProperty("durationMs", out var d) && d.TryGetDouble(out var ms) ? ms / 1000 : 0;
                long tokens = 0;
                if (meta.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
                {
                    foreach (var name in new[] { "input_tokens", "output_tokens" })
                    {
                        if (u.TryGetProperty(name, out var t) && t.TryGetInt64(out var n)) tokens += n;
                    }
                }
                var tools = new List<ToolRun>();
                if (meta.TryGetProperty("trace", out var trace) && trace.ValueKind == JsonValueKind.Array)
                {
                    foreach (var step in trace.EnumerateArray())
                    {
                        if (!step.TryGetProperty("kind", out var k) || k.GetString() != "tool") continue;
                        tools.Add(new ToolRun(
                            step.TryGetProperty("node", out var node) ? node.GetString() ?? "unknown" : "unknown",
                            !step.TryGetProperty("status", out var s) || s.GetString() == "ok",
                            step.TryGetProperty("ms", out var stepMs) && stepMs.TryGetDouble(out var v) ? v : 0));
                    }
                }
                return new AgentRun(at, intent, seconds, tokens, tools);
            }
            catch (JsonException)
            {
                return new AgentRun(at, "unknown", 0, 0, []);
            }
        }
    }

    private sealed record ToolRun(string Name, bool Ok, double Ms);

    /// <summary>A reporting period of whole UTC days, inclusive of both ends.</summary>
    private sealed record Period(DateOnly From, DateOnly To)
    {
        public DateTime StartUtc => From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        public DateTime EndUtc => To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        public int Days => To.DayNumber - From.DayNumber + 1;

        public static Period Of(ReportQuery query)
        {
            var to = query.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var from = query.From ?? to.AddDays(-(DefaultDays - 1));
            if (from > to) throw new BusinessRuleException("The report start date must be on or before the end date.");
            if (to.DayNumber - from.DayNumber >= MaxDays) throw new BusinessRuleException("Reports cover at most two years.");
            return new Period(from, to);
        }

        public IEnumerable<Month> Months()
        {
            for (var month = new DateOnly(From.Year, From.Month, 1); month <= To; month = month.AddMonths(1))
            {
                yield return new Month(month);
            }
        }
    }

    private sealed record Month(DateOnly Start)
    {
        public string Label => Start.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        public bool Contains(DateTime? at) => at is { } value && value.Year == Start.Year && value.Month == Start.Month;
    }
}
