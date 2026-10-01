namespace OpsPilot.Infrastructure.Persistence.Migrations;

/// <summary>
/// Reporting views for the ERP Query agent. Curated, snake_case and free of identity data, so a language model can
/// write correct SQL against them and a read-only login can be granted SELECT on the <c>ai</c> schema alone.
/// The schema is owned by dbo, so ownership chaining lets the views read the base tables without granting them.
/// </summary>
internal static class AiViewSql
{
    public const string CreateSchema = "IF SCHEMA_ID('ai') IS NULL EXEC('CREATE SCHEMA ai AUTHORIZATION dbo');";

    public static readonly (string Name, string Body)[] Views =
    [
        ("products", """
            SELECT p.Id AS product_id, p.Code AS code, p.Name AS name, c.Name AS category, p.UnitPrice AS unit_price, p.Cost AS cost,
                   p.ReorderPoint AS reorder_point, p.SafetyStock AS safety_stock, s.Name AS primary_supplier, p.IsActive AS is_active
            FROM dbo.Products p
            JOIN dbo.Categories c ON c.Id = p.CategoryId
            LEFT JOIN dbo.Suppliers s ON s.Id = p.PrimarySupplierId
            """),
        ("stock_levels", """
            SELECT p.Code AS product_code, p.Name AS product_name, w.Code AS warehouse_code, i.QuantityOnHand AS on_hand,
                   i.QuantityReserved AS reserved, i.QuantityOnHand - i.QuantityReserved AS available,
                   p.ReorderPoint AS reorder_point, p.SafetyStock AS safety_stock
            FROM dbo.InventoryItems i
            JOIN dbo.Products p ON p.Id = i.ProductId
            JOIN dbo.Warehouses w ON w.Id = i.WarehouseId
            """),
        ("warehouses", """
            SELECT Code AS code, Name AS name, Location AS location, IsActive AS is_active FROM dbo.Warehouses
            """),
        ("customers", """
            SELECT c.Code AS code, c.Name AS name, c.Status AS status, c.CreditLimit AS credit_limit, c.PaymentTermsDays AS payment_terms_days,
                   (SELECT TOP 1 a.City FROM dbo.CustomerAddresses a WHERE a.CustomerId = c.Id ORDER BY a.IsDefault DESC) AS city
            FROM dbo.Customers c
            """),
        ("suppliers", """
            SELECT Code AS code, Name AS name, ContactName AS contact_name, AverageLeadTimeDays AS average_lead_time_days,
                   PaymentTermsDays AS payment_terms_days, IsActive AS is_active
            FROM dbo.Suppliers
            """),
        ("sales_orders", """
            SELECT o.OrderNumber AS order_number, c.Code AS customer_code, c.Name AS customer_name, w.Code AS warehouse_code,
                   o.Status AS status, o.OrderDateUtc AS order_date, o.RequiredDateUtc AS required_date, o.ShippedAtUtc AS shipped_at,
                   o.DeliveredAtUtc AS delivered_at, o.TotalAmount AS total_amount,
                   CAST(CASE WHEN o.Status IN ('Confirmed', 'Processing') AND o.RequiredDateUtc < CAST(SYSUTCDATETIME() AS date)
                        THEN 1 ELSE 0 END AS bit) AS is_late
            FROM dbo.SalesOrders o
            JOIN dbo.Customers c ON c.Id = o.CustomerId
            JOIN dbo.Warehouses w ON w.Id = o.WarehouseId
            """),
        ("sales_order_lines", """
            SELECT o.OrderNumber AS order_number, p.Code AS product_code, p.Name AS product_name, l.Quantity AS quantity,
                   l.UnitPrice AS unit_price, l.Quantity * l.UnitPrice AS line_total
            FROM dbo.SalesOrderLines l
            JOIN dbo.SalesOrders o ON o.Id = l.SalesOrderId
            JOIN dbo.Products p ON p.Id = l.ProductId
            """),
        ("purchase_orders", """
            SELECT po.PoNumber AS po_number, s.Name AS supplier_name, w.Code AS warehouse_code, po.Status AS status,
                   po.OrderDateUtc AS order_date, po.ExpectedDeliveryDateUtc AS expected_delivery_date, po.TotalAmount AS total_amount
            FROM dbo.PurchaseOrders po
            JOIN dbo.Suppliers s ON s.Id = po.SupplierId
            JOIN dbo.Warehouses w ON w.Id = po.WarehouseId
            """),
        ("purchase_order_lines", """
            SELECT po.PoNumber AS po_number, p.Code AS product_code, p.Name AS product_name, l.Quantity AS quantity,
                   l.QuantityReceived AS quantity_received, l.Quantity - l.QuantityReceived AS quantity_outstanding, l.UnitCost AS unit_cost
            FROM dbo.PurchaseOrderLines l
            JOIN dbo.PurchaseOrders po ON po.Id = l.PurchaseOrderId
            JOIN dbo.Products p ON p.Id = l.ProductId
            """),
        ("inventory_transactions", """
            SELECT t.OccurredAtUtc AS occurred_at, p.Code AS product_code, w.Code AS warehouse_code, t.Type AS type,
                   t.Quantity AS quantity, t.Reference AS reference
            FROM dbo.InventoryTransactions t
            JOIN dbo.Products p ON p.Id = t.ProductId
            JOIN dbo.Warehouses w ON w.Id = t.WarehouseId
            """),
        ("invoices", """
            SELECT i.InvoiceNumber AS invoice_number, c.Name AS customer_name, o.OrderNumber AS order_number, i.Status AS status,
                   i.IssueDateUtc AS issue_date, i.DueDateUtc AS due_date, i.TotalAmount AS total_amount, i.AmountPaid AS amount_paid,
                   i.TotalAmount - i.AmountPaid AS balance,
                   CAST(CASE WHEN i.Status IN ('Issued', 'PartiallyPaid') AND i.DueDateUtc < CAST(SYSUTCDATETIME() AS date)
                        THEN 1 ELSE 0 END AS bit) AS is_overdue
            FROM dbo.Invoices i
            JOIN dbo.Customers c ON c.Id = i.CustomerId
            JOIN dbo.SalesOrders o ON o.Id = i.SalesOrderId
            """),
        ("payments", """
            SELECT i.InvoiceNumber AS invoice_number, p.Amount AS amount, p.PaidAtUtc AS paid_at, p.Method AS method
            FROM dbo.Payments p
            JOIN dbo.Invoices i ON i.Id = p.InvoiceId
            """),
    ];

    /// <summary>Added by the AddServiceTickets migration (kept apart so the earlier migration never references the tables).</summary>
    public static readonly (string Name, string Body)[] TicketViews =
    [
        ("service_tickets", """
            SELECT t.TicketNumber AS ticket_number, t.Subject AS subject, c.Name AS customer_name, o.OrderNumber AS order_number,
                   p.Code AS product_code, p.Name AS product_name,
                   t.Category AS category, t.Priority AS priority, t.Status AS status, t.AssignedToName AS assigned_to,
                   t.CreatedAtUtc AS created_at, t.ResolvedAtUtc AS resolved_at,
                   CAST(CASE WHEN t.Status IN ('Resolved', 'Closed') THEN 0 ELSE 1 END AS bit) AS is_open,
                   (SELECT COUNT(*) FROM dbo.TicketComments tc WHERE tc.TicketId = t.Id) AS comment_count
            FROM dbo.ServiceTickets t
            JOIN dbo.Customers c ON c.Id = t.CustomerId
            LEFT JOIN dbo.SalesOrders o ON o.Id = t.SalesOrderId
            LEFT JOIN dbo.Products p ON p.Id = t.ProductId
            """),
    ];

    public static string Create(string name, string body) => $"CREATE OR ALTER VIEW ai.{name} AS\n{body}";

    public static string Drop(string name) => $"DROP VIEW IF EXISTS ai.{name};";

    public const string DropSchema = "IF SCHEMA_ID('ai') IS NOT NULL EXEC('DROP SCHEMA ai');";
}
