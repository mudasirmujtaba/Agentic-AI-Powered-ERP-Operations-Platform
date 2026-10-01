using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Purchasing;

namespace OpsPilot.Application.Purchasing;

public record PurchaseOrderListItemDto(
    Guid Id,
    string PoNumber,
    string SupplierName,
    string WarehouseCode,
    PurchaseOrderStatus Status,
    DateTime OrderDateUtc,
    DateTime? ExpectedDeliveryDateUtc,
    decimal TotalAmount);

public record PurchaseOrderLineDto(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitCost,
    int QuantityReceived)
{
    public int QuantityRemaining => Quantity - QuantityReceived;
    public decimal LineTotal => Quantity * UnitCost;
}

public record PurchaseOrderDto(
    Guid Id,
    string PoNumber,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    string WarehouseName,
    PurchaseOrderStatus Status,
    DateTime OrderDateUtc,
    DateTime? ExpectedDeliveryDateUtc,
    DateTime? SubmittedAtUtc,
    DateTime? ApprovedAtUtc,
    DateTime? OrderedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    string? RejectionReason,
    string? Notes,
    decimal TotalAmount,
    bool RequiresApproval,
    decimal ApprovalThreshold,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

public record PurchaseOrderLineRequest(Guid ProductId, int Quantity, decimal? UnitCost);

public record SavePurchaseOrderRequest(
    Guid SupplierId,
    Guid WarehouseId,
    DateTime? ExpectedDeliveryDateUtc,
    string? Notes,
    IReadOnlyList<PurchaseOrderLineRequest> Lines);

public record RejectPurchaseOrderRequest(string Reason);

public record ReceiveLineRequest(Guid LineId, int Quantity);

public record ReceiveGoodsRequest(IReadOnlyList<ReceiveLineRequest> Lines);

public class PurchaseOrderQuery : PagedQuery
{
    public PurchaseOrderStatus? Status { get; set; }
    public Guid? SupplierId { get; set; }
}
