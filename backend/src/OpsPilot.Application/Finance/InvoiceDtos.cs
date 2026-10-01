using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Finance;

namespace OpsPilot.Application.Finance;

public record InvoiceListItemDto(
    Guid Id,
    string InvoiceNumber,
    string CustomerName,
    string SalesOrderNumber,
    InvoiceStatus Status,
    bool IsOverdue,
    DateTime? IssueDateUtc,
    DateTime? DueDateUtc,
    decimal TotalAmount,
    decimal AmountPaid)
{
    public decimal Balance => TotalAmount - AmountPaid;
}

public record InvoiceLineDto(Guid Id, string Description, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public record PaymentDto(Guid Id, decimal Amount, DateTime PaidAtUtc, PaymentMethod Method, string? Reference);

public record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    Guid SalesOrderId,
    string SalesOrderNumber,
    InvoiceStatus Status,
    bool IsOverdue,
    DateTime? IssueDateUtc,
    DateTime? DueDateUtc,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Balance,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<PaymentDto> Payments);

public record CreateInvoiceRequest(Guid SalesOrderId);

public record RecordPaymentRequest(decimal Amount, DateTime? PaidAtUtc, PaymentMethod Method, string? Reference);

public class InvoiceQuery : PagedQuery
{
    public InvoiceStatus? Status { get; set; }
    public bool OverdueOnly { get; set; }
    public Guid? CustomerId { get; set; }
}
