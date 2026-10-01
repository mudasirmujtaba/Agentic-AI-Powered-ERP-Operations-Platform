using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Sales;

namespace OpsPilot.Application.Sales;

public record SalesOrderListItemDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string WarehouseCode,
    SalesOrderStatus Status,
    DateTime OrderDateUtc,
    DateTime? RequiredDateUtc,
    decimal TotalAmount,
    bool IsLate);

public record SalesOrderLineDto(Guid Id, Guid ProductId, string ProductCode, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public record SalesOrderDto(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string CustomerName,
    Guid WarehouseId,
    string WarehouseName,
    SalesOrderStatus Status,
    DateTime OrderDateUtc,
    DateTime? RequiredDateUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? ShippedAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? CancelledAtUtc,
    string? Carrier,
    string? TrackingNumber,
    string? Notes,
    decimal TotalAmount,
    IReadOnlyList<SalesOrderLineDto> Lines,
    Guid? InvoiceId,
    string? InvoiceNumber);

public record SalesOrderLineRequest(Guid ProductId, int Quantity, decimal? UnitPrice);

public record SaveSalesOrderRequest(
    Guid CustomerId,
    Guid WarehouseId,
    DateTime? RequiredDateUtc,
    string? Notes,
    IReadOnlyList<SalesOrderLineRequest> Lines);

public record ShipSalesOrderRequest(string? Carrier, string? TrackingNumber);

public class SalesOrderQuery : PagedQuery
{
    public SalesOrderStatus? Status { get; set; }
    public Guid? CustomerId { get; set; }
    /// <summary>Open orders past their required date.</summary>
    public bool LateOnly { get; set; }
}
