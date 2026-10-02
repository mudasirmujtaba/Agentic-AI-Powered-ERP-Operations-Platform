using OpsPilot.Application.Reports;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Sales;
using OpsPilot.UnitTests.TestSupport;

namespace OpsPilot.UnitTests.Reports;

public class ReportTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly DateTime _now = DateTime.UtcNow;

    public ReportTests()
    {
        using var db = _database.NewContext();
        var product = new Product { Code = "X200", Name = "Pump", Category = new Category { Name = "Pumps" }, UnitPrice = 100m, Cost = 60m, ReorderPoint = 10 };
        var warehouse = new Warehouse { Code = "WH-MAIN", Name = "Main" };
        var apex = new Customer { Code = "C-1", Name = "Apex" };
        var birch = new Customer { Code = "C-2", Name = "Birch" };
        db.AddRange(product, warehouse, apex, birch);
        db.SaveChanges();

        var number = 10001;
        SalesOrder Order(Customer customer, int quantity, int daysAgo, bool ship)
        {
            var order = new SalesOrder { OrderNumber = $"SO-{number++}", CustomerId = customer.Id, WarehouseId = warehouse.Id, OrderDateUtc = _now.AddDays(-daysAgo) };
            order.SetLines([new SalesOrderLine { ProductId = product.Id, Quantity = quantity, UnitPrice = 100m }]);
            order.Confirm(_now);
            if (ship) order.Ship(_now, null, null);
            db.SalesOrders.Add(order);
            return order;
        }

        var shipped = Order(apex, 3, 5, ship: true);   // $300
        Order(apex, 1, 10, ship: false);               // $100
        Order(birch, 2, 20, ship: false);              // $200
        Order(birch, 5, 200, ship: false);             // outside a 90-day window
        db.SaveChanges();

        // One invoice 45 days overdue (balance $300 - $100 paid).
        var invoice = Invoice.FromOrder(shipped, "INV-30001");
        invoice.Issue(_now.AddDays(-75), 30);
        invoice.RecordPayment(100m, _now.AddDays(-2), PaymentMethod.BankTransfer, null);
        db.Invoices.Add(invoice);
        db.SaveChanges();
    }

    private Task<ReportDto> Report(string key, ReportQuery? query = null) =>
        new ReportService(_database.NewContext()).GetAsync(key, query ?? new ReportQuery());

    private static decimal? Kpi(ReportDto report, string label) => report.Kpis.Single(k => k.Label == label).Value;

    [Fact]
    public async Task Sales_report_counts_orders_in_the_period_only()
    {
        var report = await Report(ReportKeys.Sales);

        Assert.Equal(600m, Kpi(report, "Revenue"));
        Assert.Equal(3, Kpi(report, "Orders"));
        Assert.Equal(200m, Kpi(report, "Average order value"));
        Assert.Equal(2, Kpi(report, "Active customers"));
        Assert.Equal(0.4m, Kpi(report, "Gross margin")); // (600 - 6 x 60) / 600

        var byCustomer = report.Tables.Single(t => t.Title == "Revenue by customer").Rows;
        Assert.Equal("Apex", byCustomer[0]["customer"]);
        Assert.Equal(400m, byCustomer[0]["revenue"]);
        Assert.Equal(600m, report.Trend!.Points.Sum(p => p.Value));
    }

    [Fact]
    public async Task Finance_report_ages_receivables()
    {
        var report = await Report(ReportKeys.Finance);

        Assert.Equal(200m, Kpi(report, "Outstanding receivables"));
        Assert.Equal(200m, Kpi(report, "Overdue"));
        Assert.Equal(100m, Kpi(report, "Collected"));

        var aging = report.Tables.Single(t => t.Title == "Receivables aging").Rows.ToDictionary(r => (string)r["bucket"]!);
        Assert.Equal(200m, aging["31-60 days"]["balance"]);
        Assert.Equal(0m, aging["Current"]["balance"]);
    }

    [Fact]
    public async Task Every_report_builds_and_periods_are_validated()
    {
        foreach (var key in ReportKeys.All)
        {
            var report = await Report(key);
            Assert.Equal(key, report.Key);
            Assert.NotEmpty(report.Kpis);
        }

        var today = DateOnly.FromDateTime(_now);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Report(ReportKeys.Sales, new ReportQuery { From = today, To = today.AddDays(-1) }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Report(ReportKeys.Sales, new ReportQuery { From = today.AddYears(-3), To = today }));
    }

    [Fact]
    public void Each_function_reads_its_own_reports()
    {
        Assert.True(ReportKeys.CanRead(ReportKeys.Finance, ["FinanceUser"]));
        Assert.False(ReportKeys.CanRead(ReportKeys.Finance, ["SalesUser"]));
        Assert.False(ReportKeys.CanRead(ReportKeys.Ai, ["FinanceUser"]));
        Assert.True(ReportKeys.CanRead(ReportKeys.Ai, ["Manager"]));
        Assert.All(ReportKeys.All, key => Assert.True(ReportKeys.CanRead(key, ["Administrator"])));
    }

    public void Dispose() => _database.Dispose();
}
