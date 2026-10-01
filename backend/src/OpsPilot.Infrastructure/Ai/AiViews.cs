using OpsPilot.Domain.Identity;

namespace OpsPilot.Infrastructure.Ai;

/// <summary>
/// The reporting views the ERP Query agent may read, and which roles may read each. The views live in the <c>ai</c>
/// schema (see the AddAiAndAudit migration); the read-only login is granted SELECT on that schema and nothing else.
/// </summary>
public static class AiViews
{
    private static readonly string[] Everyone = [];
    private static readonly string[] FinanceRoles = [Roles.Administrator, Roles.Manager, Roles.FinanceUser];

    public static readonly IReadOnlyDictionary<string, (string Description, string[] Roles)> All =
        new Dictionary<string, (string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            ["products"] = ("Catalog: one row per product with price, cost, reorder point, safety stock and primary supplier.", Everyone),
            ["stock_levels"] = ("Current stock: one row per product per warehouse (on_hand, reserved, available).", Everyone),
            ["warehouses"] = ("Warehouses.", Everyone),
            ["customers"] = ("Customers with status (Active, OnHold, Inactive), credit limit and payment terms.", Everyone),
            ["suppliers"] = ("Suppliers with average lead time in days.", Everyone),
            ["sales_orders"] = ("Sales order headers. status: Draft, Confirmed, Processing, Shipped, Delivered, Cancelled. is_late = open and past required_date.", Everyone),
            ["sales_order_lines"] = ("Sales order lines (join on order_number).", Everyone),
            ["purchase_orders"] = ("Purchase order headers. status: Draft, PendingApproval, Approved, Ordered, PartiallyReceived, Completed, Cancelled.", Everyone),
            ["purchase_order_lines"] = ("Purchase order lines with quantity_received (join on po_number).", Everyone),
            ["inventory_transactions"] = ("Stock ledger. type: Purchase, Sale, Return, Adjustment, Transfer, Damaged. quantity is signed (sales negative).", Everyone),
            ["invoices"] = ("Invoices. status: Draft, Issued, PartiallyPaid, Paid, Cancelled. balance = total_amount - amount_paid; is_overdue computed.", FinanceRoles),
            ["payments"] = ("Payments applied to invoices (join on invoice_number).", FinanceRoles),
        };

    public static bool IsAllowed(string view, IReadOnlyList<string> userRoles) =>
        All.TryGetValue(view, out var entry) && (entry.Roles.Length == 0 || entry.Roles.Any(userRoles.Contains));

    public static IEnumerable<string> AllowedFor(IReadOnlyList<string> userRoles) =>
        All.Keys.Where(view => IsAllowed(view, userRoles)).OrderBy(v => v);
}
