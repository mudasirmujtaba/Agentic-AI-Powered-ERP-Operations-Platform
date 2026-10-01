using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpsPilot.Application.Ai;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Jobs;
using OpsPilot.Application.Notifications;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Notifications;
using OpsPilot.Domain.Sales;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.UnitTests.TestSupport;

namespace OpsPilot.UnitTests.Jobs;

public class JobTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Guid _recipient = Guid.NewGuid();
    private readonly Guid _productId;
    private readonly Guid _warehouseId;
    private int _orderNumber = 10001;

    public JobTests()
    {
        using var db = _database.NewContext();
        var product = new Product { Code = "X200", Name = "Pump", Category = new Category { Name = "Pumps" }, UnitPrice = 100m };
        var warehouse = new Warehouse { Code = "WH-MAIN", Name = "Main" };
        db.AddRange(product, warehouse);
        db.SaveChanges();
        _productId = product.Id;
        _warehouseId = warehouse.Id;
    }

    private ApplicationDbContext Db() => _database.NewContext();

    private NotificationPublisher Publisher(ApplicationDbContext db) => new(db, new FakeUserDirectory(_recipient));

    private AuditLogWriter Audit(ApplicationDbContext db) => new(db, _database.CurrentUser);

    /// <summary>A customer with one issued invoice that is <paramref name="daysOverdue"/> days past due (30-day terms).</summary>
    private async Task<(Guid CustomerId, Guid InvoiceId)> CustomerWithInvoiceAsync(string code, int daysOverdue,
        CustomerStatus status = CustomerStatus.Active, bool policyHold = false)
    {
        await using var db = Db();
        var customer = new Customer { Code = code, Name = $"Customer {code}", Status = status, OnPolicyCreditHold = policyHold };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var order = new SalesOrder
        {
            OrderNumber = $"SO-{_orderNumber++}", CustomerId = customer.Id, WarehouseId = _warehouseId, OrderDateUtc = now.AddDays(-120),
        };
        order.SetLines([new SalesOrderLine { ProductId = _productId, Quantity = 2, UnitPrice = 100m }]);
        order.Confirm(now);
        order.Ship(now, null, null);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();

        var invoice = Invoice.FromOrder(order, $"INV-{order.OrderNumber[3..]}");
        invoice.Issue(now.AddDays(-(30 + daysOverdue)), 30);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return (customer.Id, invoice.Id);
    }

    [Fact]
    public async Task Reminders_go_out_at_7_14_and_30_days_and_never_twice()
    {
        var (_, three) = await CustomerWithInvoiceAsync("C-1", 3);
        var (_, eight) = await CustomerWithInvoiceAsync("C-2", 8);
        var (_, fortyFive) = await CustomerWithInvoiceAsync("C-3", 45);

        await using (var db = Db())
        {
            var result = await new OverdueInvoiceReminderJob(db, Publisher(db), Audit(db)).RunAsync();
            Assert.Equal("2 reminders recorded across 3 overdue invoices", result.Summary);
        }

        await using (var db = Db())
        {
            var stages = await db.Invoices.ToDictionaryAsync(i => i.Id, i => i.ReminderStage);
            Assert.Equal(0, stages[three]);
            Assert.Equal(7, stages[eight]);
            Assert.Equal(30, stages[fortyFive]); // a missed run catches up to the highest stage, once
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == _recipient && n.Source == OverdueInvoiceReminderJob.Id));

            var again = await new OverdueInvoiceReminderJob(db, Publisher(db), Audit(db)).RunAsync();
            Assert.Equal("0 reminders recorded across 3 overdue invoices", again.Summary);
        }
    }

    [Fact]
    public async Task Credit_hold_applies_after_60_days_and_lifts_only_policy_holds()
    {
        var (late, _) = await CustomerWithInvoiceAsync("C-LATE", 61);
        var (recent, _) = await CustomerWithInvoiceAsync("C-RECENT", 20);
        var manualHold = Guid.Empty;
        var policyHoldPaidUp = Guid.Empty;
        await using (var db = Db())
        {
            var manual = new Customer { Code = "C-MANUAL", Name = "Manual hold", Status = CustomerStatus.OnHold };
            var paidUp = new Customer { Code = "C-PAID", Name = "Paid up", Status = CustomerStatus.OnHold, OnPolicyCreditHold = true };
            db.Customers.AddRange(manual, paidUp);
            await db.SaveChangesAsync();
            manualHold = manual.Id;
            policyHoldPaidUp = paidUp.Id;
        }

        await using (var db = Db())
        {
            var result = await new CreditHoldReviewJob(db, Publisher(db), Audit(db)).RunAsync();
            Assert.Equal("1 placed on hold, 1 released", result.Summary);
        }

        await using (var db = Db())
        {
            var customers = await db.Customers.ToDictionaryAsync(c => c.Id);
            Assert.Equal(CustomerStatus.OnHold, customers[late].Status);
            Assert.True(customers[late].OnPolicyCreditHold);
            Assert.Equal(CustomerStatus.Active, customers[recent].Status);
            Assert.Equal(CustomerStatus.OnHold, customers[manualHold].Status); // never auto-released
            Assert.Equal(CustomerStatus.Active, customers[policyHoldPaidUp].Status);
            Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "PlaceCreditHold" || a.Action == "ReleaseCreditHold"));
        }
    }

    [Fact]
    public async Task Inventory_scan_stores_a_report_and_notifies_when_products_are_at_risk()
    {
        var ai = new FakeAiClient
        {
            ScanReply = new InventoryScanReply("**X200** will run out in 4 days.", true, 20,
            [
                new InventoryRiskItem("X200", "Pump", "critical", 12, 35, 100, 4, "2026-10-06", 0, null, 14, 160),
                new InventoryRiskItem("HH-01", "Hard hat", "medium", 90, 50, 100, 30, null, 0, null, 7, 20),
            ], null),
        };

        await using var db = Db();
        var job = new InventoryRiskScanJob(db, ai, new FakeSystemPrincipal(), Publisher(db), Audit(db), NullLogger<InventoryRiskScanJob>.Instance);
        var result = await job.RunAsync();

        Assert.Equal("2 of 20 products at risk (1 critical)", result.Summary);
        Assert.Equal("system-token", ai.LastJobRequest!.AccessToken);

        var report = await new InsightService(db).GetLatestAsync("InventoryRisk");
        Assert.NotNull(report);
        Assert.Equal(2, report.ItemCount);
        Assert.Equal("X200", report.Items[0].Product);
        Assert.True(report.AiGenerated);

        var notification = await db.Notifications.SingleAsync();
        Assert.Equal(NotificationSeverity.Critical, notification.Severity);
        Assert.Equal("2 products at stock risk", notification.Title);
    }

    [Fact]
    public async Task Notification_feed_is_per_user()
    {
        _database.CurrentUser.UserId = _recipient;
        await using var db = Db();
        db.Notifications.AddRange(
            new Notification { UserId = _recipient, Title = "Mine", Body = "", Source = "test", CreatedAtUtc = DateTime.UtcNow },
            new Notification { UserId = Guid.NewGuid(), Title = "Someone else's", Body = "", Source = "test", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new NotificationService(db, _database.CurrentUser);
        var feed = await service.GetFeedAsync(20);
        Assert.Equal(["Mine"], feed.Items.Select(n => n.Title));
        Assert.Equal(1, feed.UnreadCount);

        await service.MarkAllReadAsync();
        Assert.Equal(0, (await service.GetFeedAsync(20)).UnreadCount);
    }

    public void Dispose() => _database.Dispose();
}
