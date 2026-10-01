using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Purchasing;
using OpsPilot.Domain.Sales;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Infrastructure.Persistence.Seed;

/// <summary>
/// Six months of deterministic operational history on top of the demo master data: stock levels, sales orders in
/// every status, purchase orders, invoices and payments. Current stock is set first; the ledger history (sales,
/// receipts and an opening balance) is then derived so that each item's transactions sum to its on-hand quantity.
/// Includes the design doc's scenario: X200 at 43 units with a 100-unit PO from ABC Industrial Supplies due in 12 days.
/// </summary>
internal sealed class DemoOperationsSeeder(ApplicationDbContext db)
{
    private readonly Random _random = new(20260929);
    private readonly DateTime _now = DateTime.UtcNow;
    private readonly Dictionary<(Guid ProductId, Guid WarehouseId), InventoryItem> _items = new();
    private readonly Dictionary<(Guid ProductId, Guid WarehouseId), int> _movements = new();
    private int _nextOrderNumber = 10001;
    private int _nextPoNumber = 20001;
    private int _nextInvoiceNumber = 30001;

    private Dictionary<string, Product> _products = null!;
    private Dictionary<string, Warehouse> _warehouses = null!;
    private Dictionary<string, Supplier> _suppliers = null!;
    private List<Customer> _customers = null!;

    public async Task<(int Orders, int PurchaseOrders, int Invoices)> SeedAsync()
    {
        _products = await db.Products.ToDictionaryAsync(p => p.Code);
        _warehouses = await db.Warehouses.ToDictionaryAsync(w => w.Code);
        _suppliers = await db.Suppliers.ToDictionaryAsync(s => s.Code);
        _customers = await db.Customers.OrderBy(c => c.Code).ToListAsync();

        SeedStockLevels();
        var purchaseOrders = SeedPurchaseOrders();
        var (orders, invoices) = SeedSalesOrdersAndInvoices();
        SeedOpeningBalances();

        await db.SaveChangesAsync();
        return (orders, purchaseOrders, invoices);
    }

    private void SeedStockLevels()
    {
        // Current on-hand totals; most products are healthy, a handful are deliberately low for the dashboard and the demo.
        var overrides = new Dictionary<string, int>
        {
            ["X200"] = 43, ["PS-220"] = 95, ["EM-750"] = 8, ["GL-NT"] = 260, ["TW-100"] = 0, ["CB-16A"] = 140,
        };

        foreach (var product in _products.Values)
        {
            var total = overrides.TryGetValue(product.Code, out var fixedTotal)
                ? fixedTotal
                : (int)(product.ReorderPoint * (2.2 + _random.NextDouble() * 1.3));

            var main = product.Code == "X200" ? 30 : (int)(total * 0.7);
            var east = total - main;
            var west = product.Code.StartsWith('X') || total == 0 ? 0 : east / 3;
            east -= west;

            CreateItem(product, _warehouses["WH-MAIN"], main);
            if (east > 0) CreateItem(product, _warehouses["WH-EAST"], east);
            if (west > 0) CreateItem(product, _warehouses["WH-WEST"], west);
        }
    }

    private void CreateItem(Product product, Warehouse warehouse, int quantity)
    {
        var item = new InventoryItem(product.Id, warehouse.Id);
        if (quantity > 0) item.Receive(quantity);
        db.InventoryItems.Add(item);
        _items[(product.Id, warehouse.Id)] = item;
    }

    private int SeedPurchaseOrders()
    {
        var count = 0;
        var main = _warehouses["WH-MAIN"];

        // Fully received history.
        foreach (var (supplier, daysAgo, lines) in new[]
        {
            ("SUP-FST", 150, new[] { ("BT-M10", 600), ("NT-M10", 600) }),
            ("SUP-VOL", 120, new[] { ("CB-16A", 300), ("CT-40A", 180) }),
            ("SUP-HYD", 95, new[] { ("VB-050", 200), ("VG-100", 120) }),
            ("SUP-ABC", 70, new[] { ("X200", 150), ("X100", 80) }),
            ("SUP-GLB", 45, new[] { ("HH-01", 400), ("HV-XL", 250) }),
            ("SUP-PRC", 35, new[] { ("DR-18V", 40), ("AG-115", 35) }),
        })
        {
            var po = NewPurchaseOrder(supplier, main, daysAgo, lines);
            ApproveIfNeeded(po, daysAgo);
            po.MarkOrdered(_now.AddDays(-daysAgo + 1));
            var receivedAt = _now.AddDays(-daysAgo + _suppliers[supplier].AverageLeadTimeDays + 1);
            foreach (var line in po.Lines)
            {
                po.Receive(line, line.Quantity, receivedAt);
                RecordMovement(line.ProductId, main.Id, InventoryTransactionType.Purchase, line.Quantity, po.PoNumber, receivedAt);
            }
            count++;
        }

        // Partially received: 120 of 200 pressure sensors arrived.
        var partial = NewPurchaseOrder("SUP-NOR", main, 14, [("PS-220", 200)]);
        ApproveIfNeeded(partial, 14);
        partial.MarkOrdered(_now.AddDays(-13));
        partial.Receive(partial.Lines.First(), 120, _now.AddDays(-3));
        RecordMovement(partial.Lines.First().ProductId, main.Id, InventoryTransactionType.Purchase, 120, partial.PoNumber, _now.AddDays(-3));
        count++;

        // The design doc's scenario: 100 X200 pumps on order from ABC, expected in 12 days.
        var pumps = NewPurchaseOrder("SUP-ABC", main, 2, [("X200", 100)]);
        pumps.ExpectedDeliveryDateUtc = _now.Date.AddDays(12);
        ApproveIfNeeded(pumps, 2);
        pumps.MarkOrdered(_now.AddDays(-2));
        count++;

        // Above the $10,000 threshold, waiting for a manager.
        NewPurchaseOrder("SUP-VOL", main, 1, [("EM-750", 30)]).Submit(_now.AddDays(-1));
        count++;

        // Small order, approved automatically on submit, not yet sent to the supplier.
        NewPurchaseOrder("SUP-GLB", main, 1, [("GL-NT", 600), ("SG-02", 300)]).Submit(_now.AddHours(-20));
        count++;

        NewPurchaseOrder("SUP-MRD", main, 0, [("TW-100", 40)]);
        count++;

        NewPurchaseOrder("SUP-PRC", main, 60, [("TW-100", 25)]).Cancel(_now.AddDays(-58));
        count++;

        return count;
    }

    private PurchaseOrder NewPurchaseOrder(string supplierCode, Warehouse warehouse, int daysAgo, (string Code, int Quantity)[] lines)
    {
        var supplier = _suppliers[supplierCode];
        var orderDate = _now.AddDays(-daysAgo);
        var po = new PurchaseOrder
        {
            PoNumber = $"PO-{_nextPoNumber++}",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            OrderDateUtc = orderDate,
            ExpectedDeliveryDateUtc = orderDate.Date.AddDays(supplier.AverageLeadTimeDays),
        };
        po.SetLines(lines.Select(l => new PurchaseOrderLine
        {
            ProductId = _products[l.Code].Id,
            Product = _products[l.Code],
            Quantity = l.Quantity,
            UnitCost = _products[l.Code].Cost,
        }));
        db.PurchaseOrders.Add(po);
        return po;
    }

    private void ApproveIfNeeded(PurchaseOrder po, int daysAgo)
    {
        po.Submit(_now.AddDays(-daysAgo));
        if (po.Status == PurchaseOrderStatus.PendingApproval)
        {
            po.Approve(null, _now.AddDays(-daysAgo).AddHours(4));
        }
    }

    private (int Orders, int Invoices) SeedSalesOrdersAndInvoices()
    {
        var invoices = 0;
        var orderable = _customers.Where(c => c.Status != CustomerStatus.Inactive).ToList();
        var popular = new[] { "X200", "X100", "VB-050", "PS-220", "CB-16A", "HH-01", "GL-NT", "BT-M10" };
        var days = Enumerable.Range(0, 48).Select(_ => _random.Next(0, 175)).OrderByDescending(d => d).ToList();

        foreach (var daysAgo in days)
        {
            var status = PickStatus(daysAgo);
            var holdsStock = status is SalesOrderStatus.Confirmed or SalesOrderStatus.Processing;

            var candidates = orderable.Where(c => c.Status == CustomerStatus.Active || daysAgo > 60).ToList();
            var customer = candidates[_random.Next(candidates.Count)];
            var warehouse = holdsStock || _random.NextDouble() < 0.8 ? _warehouses["WH-MAIN"] : _warehouses["WH-EAST"];
            var orderDate = _now.AddDays(-daysAgo).AddHours(-_random.Next(0, 8));

            var lines = BuildLines(popular, warehouse, holdsStock);
            if (lines.Count == 0)
            {
                status = SalesOrderStatus.Draft;
                lines = BuildLines(popular, warehouse, reserve: false);
            }

            var order = new SalesOrder
            {
                OrderNumber = $"SO-{_nextOrderNumber++}",
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                OrderDateUtc = orderDate,
                RequiredDateUtc = orderDate.Date.AddDays(_random.Next(5, 12)),
            };
            order.SetLines(lines);
            db.SalesOrders.Add(order);

            ApplyStatus(order, status, orderDate, warehouse);

            if (order.Status is SalesOrderStatus.Shipped or SalesOrderStatus.Delivered)
            {
                SeedInvoice(order, customer);
                invoices++;
            }
        }

        return (days.Count, invoices);
    }

    private SalesOrderStatus PickStatus(int daysAgo)
    {
        var roll = _random.NextDouble();
        return daysAgo switch
        {
            > 40 => roll < 0.94 ? SalesOrderStatus.Delivered : SalesOrderStatus.Cancelled,
            > 15 => roll < 0.6 ? SalesOrderStatus.Delivered : SalesOrderStatus.Shipped,
            > 5 => roll < 0.3 ? SalesOrderStatus.Shipped : roll < 0.7 ? SalesOrderStatus.Processing : SalesOrderStatus.Confirmed,
            _ => roll < 0.4 ? SalesOrderStatus.Confirmed : roll < 0.8 ? SalesOrderStatus.Draft : SalesOrderStatus.Processing,
        };
    }

    private List<SalesOrderLine> BuildLines(string[] popular, Warehouse warehouse, bool reserve)
    {
        var lineCount = _random.Next(1, 4);
        var codes = new HashSet<string>();
        while (codes.Count < lineCount)
        {
            codes.Add(_random.NextDouble() < 0.6 ? popular[_random.Next(popular.Length)] : _products.Keys.ElementAt(_random.Next(_products.Count)));
        }

        var lines = new List<SalesOrderLine>();
        foreach (var code in codes)
        {
            var product = _products[code];
            var quantity = product.UnitPrice switch
            {
                > 300 => _random.Next(1, 4),
                > 100 => _random.Next(2, 12),
                > 30 => _random.Next(5, 30),
                _ => _random.Next(20, 120),
            };

            if (reserve)
            {
                // Open orders reserve stock, so keep them within what is on the shelf.
                if (!_items.TryGetValue((product.Id, warehouse.Id), out var item) || item.QuantityAvailable < 2) continue;
                quantity = Math.Min(quantity, item.QuantityAvailable / 3);
                if (quantity <= 0) continue;
                item.Reserve(quantity, product.Code);
            }

            lines.Add(new SalesOrderLine { ProductId = product.Id, Product = product, Quantity = quantity, UnitPrice = product.UnitPrice });
        }

        return lines;
    }

    private void ApplyStatus(SalesOrder order, SalesOrderStatus status, DateTime orderDate, Warehouse warehouse)
    {
        if (status == SalesOrderStatus.Draft) return;

        if (status == SalesOrderStatus.Cancelled)
        {
            order.Cancel(orderDate.AddDays(1));
            return;
        }

        order.Confirm(orderDate.AddHours(2));
        if (status == SalesOrderStatus.Confirmed) return;

        order.StartProcessing();
        if (status == SalesOrderStatus.Processing) return;

        var shippedAt = Earliest(orderDate.AddDays(_random.Next(1, 5)), _now.AddHours(-2));
        var carrier = _random.NextDouble() < 0.5 ? "UPS" : "FedEx";
        order.Ship(shippedAt, carrier, $"1Z{_random.Next(100000, 999999)}{_random.Next(1000, 9999)}");
        foreach (var line in order.Lines)
        {
            RecordMovement(line.ProductId, warehouse.Id, InventoryTransactionType.Sale, -line.Quantity, order.OrderNumber, shippedAt);
        }
        if (status == SalesOrderStatus.Shipped) return;

        order.Deliver(Earliest(shippedAt.AddDays(_random.Next(2, 5)), _now.AddHours(-1)));
    }

    private void SeedInvoice(SalesOrder order, Customer customer)
    {
        var invoice = Invoice.FromOrder(order, $"INV-{_nextInvoiceNumber++}");
        var issuedAt = order.ShippedAtUtc!.Value.AddHours(6);
        invoice.Issue(issuedAt, customer.PaymentTermsDays);

        var due = invoice.DueDateUtc!.Value;
        var roll = _random.NextDouble();
        var paidAt = Earliest(due.AddDays(-_random.Next(0, 12)), _now.AddHours(-1));
        if (paidAt < issuedAt) paidAt = Earliest(issuedAt.AddDays(3), _now.AddHours(-1));

        var (paidFraction, record) = (due - _now).TotalDays switch
        {
            < -20 => roll < 0.85 ? (1m, true) : roll < 0.95 ? (0.5m, true) : (0m, false),
            < 0 => roll < 0.5 ? (1m, true) : roll < 0.7 ? (0.4m, true) : (0m, false),
            _ => roll < 0.3 ? (1m, true) : (0m, false),
        };

        if (record && paidAt > issuedAt)
        {
            var amount = Math.Round(invoice.TotalAmount * paidFraction, 2);
            if (amount > 0)
            {
                invoice.RecordPayment(amount, paidAt, _random.NextDouble() < 0.7 ? PaymentMethod.BankTransfer : PaymentMethod.Card,
                    $"REF-{_random.Next(100000, 999999)}");
            }
        }

        db.Invoices.Add(invoice);
    }

    private void SeedOpeningBalances()
    {
        var openingDate = _now.AddDays(-200);
        foreach (var ((productId, warehouseId), item) in _items)
        {
            var opening = item.QuantityOnHand - _movements.GetValueOrDefault((productId, warehouseId));
            if (opening > 0)
            {
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Type = InventoryTransactionType.Adjustment,
                    Quantity = opening,
                    Reference = "Opening balance",
                    OccurredAtUtc = openingDate,
                });
            }
        }
    }

    private void RecordMovement(Guid productId, Guid warehouseId, InventoryTransactionType type, int signedQuantity, string reference, DateTime at)
    {
        _movements[(productId, warehouseId)] = _movements.GetValueOrDefault((productId, warehouseId)) + signedQuantity;
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = signedQuantity,
            Reference = reference,
            OccurredAtUtc = at,
        });
    }

    private static DateTime Earliest(DateTime a, DateTime b) => a < b ? a : b;
}
