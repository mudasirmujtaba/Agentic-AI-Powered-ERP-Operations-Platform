using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Inventory;

namespace OpsPilot.Domain.Sales;

public class SalesOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Draft;
    public DateTime OrderDateUtc { get; set; }
    public DateTime? RequiredDateUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? ShippedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? Carrier { get; private set; }
    public string? TrackingNumber { get; private set; }
    public string? Notes { get; set; }

    public decimal TotalAmount { get; private set; }
    public ICollection<SalesOrderLine> Lines { get; private set; } = new List<SalesOrderLine>();

    /// <summary>Confirmed and processing orders hold stock reservations; shipping consumes them, cancelling releases them.</summary>
    public bool HoldsReservation => Status is SalesOrderStatus.Confirmed or SalesOrderStatus.Processing;

    public bool IsOpen => Status is SalesOrderStatus.Confirmed or SalesOrderStatus.Processing or SalesOrderStatus.Shipped;

    public void SetLines(IEnumerable<SalesOrderLine> lines)
    {
        EnsureStatus("edit", SalesOrderStatus.Draft);
        Lines.Clear();
        foreach (var line in lines)
        {
            Lines.Add(line);
        }
        if (Lines.Count == 0)
        {
            throw new BusinessRuleException("An order needs at least one line.");
        }
        TotalAmount = Lines.Sum(l => l.Quantity * l.UnitPrice);
    }

    public void Confirm(DateTime nowUtc)
    {
        EnsureStatus("confirm", SalesOrderStatus.Draft);
        Status = SalesOrderStatus.Confirmed;
        ConfirmedAtUtc = nowUtc;
    }

    public void StartProcessing()
    {
        EnsureStatus("start processing", SalesOrderStatus.Confirmed);
        Status = SalesOrderStatus.Processing;
    }

    public void Ship(DateTime nowUtc, string? carrier, string? trackingNumber)
    {
        EnsureStatus("ship", SalesOrderStatus.Confirmed, SalesOrderStatus.Processing);
        Status = SalesOrderStatus.Shipped;
        ShippedAtUtc = nowUtc;
        Carrier = carrier;
        TrackingNumber = trackingNumber;
    }

    public void Deliver(DateTime nowUtc)
    {
        EnsureStatus("mark as delivered", SalesOrderStatus.Shipped);
        Status = SalesOrderStatus.Delivered;
        DeliveredAtUtc = nowUtc;
    }

    public void Cancel(DateTime nowUtc)
    {
        EnsureStatus("cancel", SalesOrderStatus.Draft, SalesOrderStatus.Confirmed, SalesOrderStatus.Processing);
        Status = SalesOrderStatus.Cancelled;
        CancelledAtUtc = nowUtc;
    }

    private void EnsureStatus(string action, params SalesOrderStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new BusinessRuleException(
                $"Cannot {action} order {OrderNumber} while it is {Status}; allowed only when {string.Join(" or ", allowed)}.");
        }
    }
}

public class SalesOrderLine
{
    public Guid Id { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public enum SalesOrderStatus
{
    Draft,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
