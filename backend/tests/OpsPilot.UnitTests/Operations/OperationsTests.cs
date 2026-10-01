using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Finance;
using OpsPilot.Application.Inventory;
using OpsPilot.Application.Purchasing;
using OpsPilot.Application.Sales;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Purchasing;
using OpsPilot.Domain.Sales;
using OpsPilot.Domain.Suppliers;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.UnitTests.TestSupport;

namespace OpsPilot.UnitTests.Operations;

/// <summary>End-to-end rules across stock, sales, purchasing and invoicing against a real (SQLite) database.</summary>
public class OperationsTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Guid _productId;
    private readonly Guid _warehouseId;
    private readonly Guid _customerId;
    private readonly Guid _supplierId;

    public OperationsTests()
    {
        using var db = _database.NewContext();
        var category = new Category { Name = "Pumps" };
        var supplier = new Supplier { Code = "SUP-ABC", Name = "ABC", AverageLeadTimeDays = 7 };
        var product = new Product { Code = "X200", Name = "Pump", Category = category, UnitPrice = 100m, Cost = 60m, ReorderPoint = 100, SafetyStock = 35 };
        var warehouse = new Warehouse { Code = "WH-MAIN", Name = "Main" };
        var customer = new Customer { Code = "CUST-1", Name = "Apex", CreditLimit = 5_000m, PaymentTermsDays = 30 };
        db.AddRange(category, supplier, product, warehouse, customer);
        db.SaveChanges();

        _productId = product.Id;
        _warehouseId = warehouse.Id;
        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }

    private ApplicationDbContext Db() => _database.NewContext();

    private AuditLogWriter Audit(ApplicationDbContext db) => new(db, _database.CurrentUser);

    private SalesOrderService Sales(ApplicationDbContext db) => new(db, new StockLedger(db), Audit(db), new SaveSalesOrderRequestValidator());

    private PurchaseOrderService Purchasing(ApplicationDbContext db) => new(db, new StockLedger(db), Audit(db), _database.CurrentUser,
        new SavePurchaseOrderRequestValidator(), new RejectPurchaseOrderRequestValidator(), new ReceiveGoodsRequestValidator());

    private InventoryService Inventory(ApplicationDbContext db) =>
        new(db, new StockLedger(db), Audit(db), new AdjustStockRequestValidator(), new TransferStockRequestValidator());

    private async Task StockAsync(int quantity)
    {
        await using var db = Db();
        await Inventory(db).AdjustAsync(new AdjustStockRequest(_productId, _warehouseId, quantity, InventoryTransactionType.Adjustment, "test"));
    }

    private async Task<SalesOrderDto> DraftOrderAsync(int quantity)
    {
        await using var db = Db();
        return await Sales(db).CreateAsync(new SaveSalesOrderRequest(_customerId, _warehouseId, null, null,
            [new SalesOrderLineRequest(_productId, quantity, null)]));
    }

    private async Task<InventoryItem> ItemAsync()
    {
        await using var db = Db();
        return await db.InventoryItems.AsNoTracking().SingleAsync(i => i.ProductId == _productId);
    }

    [Fact]
    public async Task ConfirmingAnOrder_ReservesStock_AndShippingDeductsIt()
    {
        await StockAsync(50);
        var order = await DraftOrderAsync(10);
        Assert.Equal(1_000m, order.TotalAmount);

        await using (var db = Db()) await Sales(db).ConfirmAsync(order.Id);
        var afterConfirm = await ItemAsync();
        Assert.Equal(50, afterConfirm.QuantityOnHand);
        Assert.Equal(10, afterConfirm.QuantityReserved);

        await using (var db = Db()) await Sales(db).ShipAsync(order.Id, new ShipSalesOrderRequest("UPS", "1Z"));
        var afterShip = await ItemAsync();
        Assert.Equal(40, afterShip.QuantityOnHand);
        Assert.Equal(0, afterShip.QuantityReserved);

        await using var check = Db();
        Assert.Contains(check.InventoryTransactions, t => t.Type == InventoryTransactionType.Sale && t.Quantity == -10);
    }

    [Fact]
    public async Task ConfirmingWithoutEnoughStock_IsRejectedAndReservesNothing()
    {
        await StockAsync(5);
        var order = await DraftOrderAsync(10);

        await using var db = Db();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Sales(db).ConfirmAsync(order.Id));
        Assert.Contains("X200", error.Message);
        Assert.Equal(0, (await ItemAsync()).QuantityReserved);
    }

    [Fact]
    public async Task ConfirmingOverTheCreditLimit_IsRejected()
    {
        await StockAsync(100);
        var order = await DraftOrderAsync(60); // 6,000 against a 5,000 limit

        await using var db = Db();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Sales(db).ConfirmAsync(order.Id));
        Assert.Contains("credit limit", error.Message);
    }

    [Fact]
    public async Task CancellingAConfirmedOrder_ReleasesTheReservation()
    {
        await StockAsync(50);
        var order = await DraftOrderAsync(10);
        await using (var db = Db()) await Sales(db).ConfirmAsync(order.Id);

        await using (var db = Db()) await Sales(db).CancelAsync(order.Id);

        Assert.Equal(0, (await ItemAsync()).QuantityReserved);
    }

    [Fact]
    public async Task ShippedOrders_CannotBeCancelled()
    {
        await StockAsync(50);
        var order = await DraftOrderAsync(10);
        await using (var db = Db()) await Sales(db).ConfirmAsync(order.Id);
        await using (var db = Db()) await Sales(db).ShipAsync(order.Id, new ShipSalesOrderRequest(null, null));

        await using var check = Db();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Sales(check).CancelAsync(order.Id));
    }

    [Fact]
    public async Task PurchaseOrdersAboveTheThreshold_NeedApproval_AndReceiptsAddStock()
    {
        PurchaseOrderDto po;
        await using (var db = Db())
        {
            po = await Purchasing(db).CreateAsync(new SavePurchaseOrderRequest(_supplierId, _warehouseId, null, null,
                [new PurchaseOrderLineRequest(_productId, 200, null)])); // 200 x 60 = 12,000
        }
        Assert.True(po.RequiresApproval);

        await using (var db = Db()) po = await Purchasing(db).SubmitAsync(po.Id);
        Assert.Equal(PurchaseOrderStatus.PendingApproval, po.Status);

        await using (var db = Db()) await Purchasing(db).ApproveAsync(po.Id);
        await using (var db = Db()) await Purchasing(db).MarkOrderedAsync(po.Id);

        var lineId = po.Lines.Single().Id;
        await using (var db = Db()) po = await Purchasing(db).ReceiveAsync(po.Id, new ReceiveGoodsRequest([new ReceiveLineRequest(lineId, 120)]));
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, po.Status);
        Assert.Equal(120, (await ItemAsync()).QuantityOnHand);

        await using (var db = Db()) po = await Purchasing(db).ReceiveAsync(po.Id, new ReceiveGoodsRequest([new ReceiveLineRequest(lineId, 80)]));
        Assert.Equal(PurchaseOrderStatus.Completed, po.Status);
        Assert.Equal(200, (await ItemAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task SmallPurchaseOrders_AreApprovedOnSubmit()
    {
        await using var db = Db();
        var service = Purchasing(db);
        var po = await service.CreateAsync(new SavePurchaseOrderRequest(_supplierId, _warehouseId, null, null,
            [new PurchaseOrderLineRequest(_productId, 10, null)]));

        po = await service.SubmitAsync(po.Id);

        Assert.Equal(PurchaseOrderStatus.Approved, po.Status);
    }

    [Fact]
    public async Task InvoicingAShippedOrder_ThenPaying_SettlesTheInvoice()
    {
        await StockAsync(50);
        var order = await DraftOrderAsync(10);
        await using (var db = Db()) await Sales(db).ConfirmAsync(order.Id);
        await using (var db = Db()) await Sales(db).ShipAsync(order.Id, new ShipSalesOrderRequest(null, null));

        InvoiceDto invoice;
        await using (var db = Db())
        {
            var service = new InvoiceService(db, Audit(db), new RecordPaymentRequestValidator());
            invoice = await service.CreateFromOrderAsync(new CreateInvoiceRequest(order.Id));
            await Assert.ThrowsAsync<OpsPilot.Application.Common.Exceptions.ConflictException>(
                () => service.CreateFromOrderAsync(new CreateInvoiceRequest(order.Id)));
            invoice = await service.IssueAsync(invoice.Id);
        }
        Assert.Equal(DateTime.UtcNow.Date.AddDays(30), invoice.DueDateUtc);

        await using (var db = Db())
        {
            var service = new InvoiceService(db, Audit(db), new RecordPaymentRequestValidator());
            invoice = await service.RecordPaymentAsync(invoice.Id, new RecordPaymentRequest(400m, null, PaymentMethod.BankTransfer, null));
            Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.RecordPaymentAsync(invoice.Id, new RecordPaymentRequest(700m, null, PaymentMethod.BankTransfer, null)));
        }

        await using (var db = Db())
        {
            invoice = await new InvoiceService(db, Audit(db), new RecordPaymentRequestValidator())
                .RecordPaymentAsync(invoice.Id, new RecordPaymentRequest(600m, null, PaymentMethod.Card, null));
        }
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0m, invoice.Balance);
        Assert.Equal(2, invoice.Payments.Count);
    }

    [Fact]
    public async Task RemovingMoreThanAvailable_IsRejected()
    {
        await StockAsync(5);

        await using var db = Db();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Inventory(db).AdjustAsync(
            new AdjustStockRequest(_productId, _warehouseId, -6, InventoryTransactionType.Damaged, null)));
    }

    public void Dispose() => _database.Dispose();
}
