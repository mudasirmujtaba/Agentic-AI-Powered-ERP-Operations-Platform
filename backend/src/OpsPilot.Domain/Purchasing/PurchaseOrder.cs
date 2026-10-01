using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Domain.Purchasing;

public class PurchaseOrder : BaseEntity
{
    /// <summary>Orders above this total need a manager's approval (company procurement policy).</summary>
    public const decimal ApprovalThreshold = 10_000m;

    public string PoNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public DateTime OrderDateUtc { get; set; }
    public DateTime? ExpectedDeliveryDateUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? OrderedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? Notes { get; set; }

    public decimal TotalAmount { get; private set; }
    public ICollection<PurchaseOrderLine> Lines { get; private set; } = new List<PurchaseOrderLine>();

    public bool RequiresApproval => TotalAmount > ApprovalThreshold;

    public void SetLines(IEnumerable<PurchaseOrderLine> lines)
    {
        EnsureStatus("edit", PurchaseOrderStatus.Draft);
        Lines.Clear();
        foreach (var line in lines)
        {
            Lines.Add(line);
        }
        if (Lines.Count == 0)
        {
            throw new BusinessRuleException("A purchase order needs at least one line.");
        }
        TotalAmount = Lines.Sum(l => l.Quantity * l.UnitCost);
    }

    /// <summary>Small orders are approved automatically; large ones wait for a manager.</summary>
    public void Submit(DateTime nowUtc)
    {
        EnsureStatus("submit", PurchaseOrderStatus.Draft);
        SubmittedAtUtc = nowUtc;
        RejectionReason = null;
        if (RequiresApproval)
        {
            Status = PurchaseOrderStatus.PendingApproval;
        }
        else
        {
            Status = PurchaseOrderStatus.Approved;
            ApprovedAtUtc = nowUtc;
        }
    }

    public void Approve(Guid? approverId, DateTime nowUtc)
    {
        EnsureStatus("approve", PurchaseOrderStatus.PendingApproval);
        Status = PurchaseOrderStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAtUtc = nowUtc;
    }

    public void Reject(string reason)
    {
        EnsureStatus("reject", PurchaseOrderStatus.PendingApproval);
        Status = PurchaseOrderStatus.Draft;
        RejectionReason = reason;
    }

    public void MarkOrdered(DateTime nowUtc)
    {
        EnsureStatus("mark as ordered", PurchaseOrderStatus.Approved);
        Status = PurchaseOrderStatus.Ordered;
        OrderedAtUtc = nowUtc;
    }

    public void Receive(PurchaseOrderLine line, int quantity, DateTime nowUtc)
    {
        EnsureStatus("receive goods for", PurchaseOrderStatus.Ordered, PurchaseOrderStatus.PartiallyReceived);
        if (quantity <= 0 || quantity > line.QuantityRemaining)
        {
            throw new BusinessRuleException(
                $"Received quantity must be between 1 and the {line.QuantityRemaining} still outstanding on this line.");
        }

        line.QuantityReceived += quantity;

        if (Lines.All(l => l.QuantityRemaining == 0))
        {
            Status = PurchaseOrderStatus.Completed;
            CompletedAtUtc = nowUtc;
        }
        else
        {
            Status = PurchaseOrderStatus.PartiallyReceived;
        }
    }

    public void Cancel(DateTime nowUtc)
    {
        EnsureStatus("cancel", PurchaseOrderStatus.Draft, PurchaseOrderStatus.PendingApproval,
            PurchaseOrderStatus.Approved, PurchaseOrderStatus.Ordered);
        Status = PurchaseOrderStatus.Cancelled;
        CancelledAtUtc = nowUtc;
    }

    private void EnsureStatus(string action, params PurchaseOrderStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new BusinessRuleException(
                $"Cannot {action} {PoNumber} while it is {Status}; allowed only when {string.Join(" or ", allowed)}.");
        }
    }
}

public class PurchaseOrderLine
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public int QuantityReceived { get; set; }
    public int QuantityRemaining => Quantity - QuantityReceived;
}

public enum PurchaseOrderStatus
{
    Draft,
    PendingApproval,
    Approved,
    Ordered,
    PartiallyReceived,
    Completed,
    Cancelled
}
