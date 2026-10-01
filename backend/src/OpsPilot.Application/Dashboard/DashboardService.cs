using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Inventory;
using OpsPilot.Application.Sales;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Purchasing;
using OpsPilot.Domain.Sales;

namespace OpsPilot.Application.Dashboard;

public record MonthlyRevenueDto(int Year, int Month, decimal Revenue);

public record TopCustomerDto(Guid CustomerId, string CustomerName, decimal Revenue);

public record RecentOrderDto(Guid Id, string OrderNumber, string CustomerName, SalesOrderStatus Status, DateTime OrderDateUtc, decimal TotalAmount);

public record DashboardSummaryDto(
    decimal RevenueLast30Days,
    decimal RevenueYearToDate,
    int OrdersLast30Days,
    int OpenSalesOrders,
    int LateSalesOrders,
    int LowStockProducts,
    int OutOfStockProducts,
    decimal OutstandingReceivables,
    int OverdueInvoices,
    decimal OverdueAmount,
    int PendingPurchaseApprovals,
    int OpenPurchaseOrders,
    IReadOnlyList<MonthlyRevenueDto> RevenueByMonth,
    IReadOnlyList<TopCustomerDto> TopCustomers,
    IReadOnlyList<StockLevelDto> LowStock,
    IReadOnlyList<RecentOrderDto> RecentOrders);

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>Revenue is invoiced revenue: issued, partially paid and paid invoices, by issue date.</summary>
public class DashboardService(IApplicationDbContext db, IInventoryService inventory) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var last30 = today.AddDays(-30);
        var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var sixMonthsStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

        var billed = db.Invoices.AsNoTracking().Where(i =>
            i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Paid);

        var billedSinceSixMonths = await billed
            .Where(i => i.IssueDateUtc >= sixMonthsStart || i.IssueDateUtc >= yearStart)
            .Select(i => new { i.IssueDateUtc, i.TotalAmount })
            .ToListAsync(cancellationToken);

        var outstanding = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            .Select(i => new { Balance = i.TotalAmount - i.AmountPaid, i.DueDateUtc })
            .ToListAsync(cancellationToken);

        var revenueByMonth = Enumerable.Range(0, 6)
            .Select(offset => sixMonthsStart.AddMonths(offset))
            .Select(month => new MonthlyRevenueDto(
                month.Year,
                month.Month,
                billedSinceSixMonths
                    .Where(i => i.IssueDateUtc is { } d && d.Year == month.Year && d.Month == month.Month)
                    .Sum(i => i.TotalAmount)))
            .ToList();

        var yearAgo = today.AddYears(-1);
        var customerRevenue = await billed
            .Where(i => i.IssueDateUtc >= yearAgo)
            .Select(i => new { i.CustomerId, i.Customer.Name, i.TotalAmount })
            .ToListAsync(cancellationToken);

        var topCustomers = customerRevenue
            .GroupBy(i => new { i.CustomerId, i.Name })
            .Select(g => new TopCustomerDto(g.Key.CustomerId, g.Key.Name, g.Sum(i => i.TotalAmount)))
            .OrderByDescending(c => c.Revenue)
            .Take(5)
            .ToList();

        var lowStock = await inventory.ListStockAsync(
            new StockQuery { LowStockOnly = true, PageSize = 100, SortBy = "quantityAvailable" }, cancellationToken);

        var recentOrders = await db.SalesOrders.AsNoTracking()
            .OrderByDescending(o => o.OrderDateUtc)
            .Take(6)
            .Select(o => new RecentOrderDto(o.Id, o.OrderNumber, o.Customer.Name, o.Status, o.OrderDateUtc, o.TotalAmount))
            .ToListAsync(cancellationToken);

        var overdue = outstanding.Where(i => i.DueDateUtc < today).ToList();

        return new DashboardSummaryDto(
            RevenueLast30Days: billedSinceSixMonths.Where(i => i.IssueDateUtc >= last30).Sum(i => i.TotalAmount),
            RevenueYearToDate: billedSinceSixMonths.Where(i => i.IssueDateUtc >= yearStart).Sum(i => i.TotalAmount),
            OrdersLast30Days: await db.SalesOrders.CountAsync(
                o => o.OrderDateUtc >= last30 && o.Status != SalesOrderStatus.Cancelled, cancellationToken),
            OpenSalesOrders: await db.SalesOrders.CountAsync(
                o => o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing || o.Status == SalesOrderStatus.Shipped,
                cancellationToken),
            LateSalesOrders: await db.SalesOrders.CountAsync(SalesOrderService.IsLate(today), cancellationToken),
            LowStockProducts: lowStock.TotalCount,
            OutOfStockProducts: lowStock.Items.Count(s => s.QuantityAvailable <= 0),
            OutstandingReceivables: outstanding.Sum(i => i.Balance),
            OverdueInvoices: overdue.Count,
            OverdueAmount: overdue.Sum(i => i.Balance),
            PendingPurchaseApprovals: await db.PurchaseOrders.CountAsync(o => o.Status == PurchaseOrderStatus.PendingApproval, cancellationToken),
            OpenPurchaseOrders: await db.PurchaseOrders.CountAsync(
                o => o.Status == PurchaseOrderStatus.Approved || o.Status == PurchaseOrderStatus.Ordered || o.Status == PurchaseOrderStatus.PartiallyReceived,
                cancellationToken),
            RevenueByMonth: revenueByMonth,
            TopCustomers: topCustomers,
            LowStock: lowStock.Items.Take(8).ToList(),
            RecentOrders: recentOrders);
    }
}
