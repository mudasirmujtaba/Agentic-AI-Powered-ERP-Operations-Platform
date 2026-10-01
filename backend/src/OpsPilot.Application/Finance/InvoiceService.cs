using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Sales;

namespace OpsPilot.Application.Finance;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceListItemDto>> ListAsync(InvoiceQuery query, CancellationToken cancellationToken = default);
    Task<InvoiceDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InvoiceDto> CreateFromOrderAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceDto> IssueAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InvoiceDto> RecordPaymentAsync(Guid id, RecordPaymentRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

public class InvoiceService(IApplicationDbContext db, AuditLogWriter audit, IValidator<RecordPaymentRequest> paymentValidator) : IInvoiceService
{
    private static readonly Dictionary<string, Expression<Func<Invoice, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["invoiceNumber"] = i => i.InvoiceNumber,
            ["customerName"] = i => i.Customer.Name,
            ["status"] = i => i.Status,
            ["issueDateUtc"] = i => i.IssueDateUtc!,
            ["dueDateUtc"] = i => i.DueDateUtc!,
            ["totalAmount"] = i => i.TotalAmount,
        };

    public async Task<PagedResult<InvoiceListItemDto>> ListAsync(InvoiceQuery query, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var invoices = db.Invoices.AsNoTracking();

        if (query.Status is { } status) invoices = invoices.Where(i => i.Status == status);
        if (query.CustomerId is { } customerId) invoices = invoices.Where(i => i.CustomerId == customerId);
        if (query.OverdueOnly)
        {
            invoices = invoices.Where(i =>
                (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDateUtc < today);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            invoices = invoices.Where(i =>
                i.InvoiceNumber.Contains(search) || i.Customer.Name.Contains(search) || i.SalesOrder.OrderNumber.Contains(search));
        }

        return await invoices
            .ApplySort(query, SortMap, "invoiceNumber")
            .Select(i => new InvoiceListItemDto(
                i.Id, i.InvoiceNumber, i.Customer.Name, i.SalesOrder.OrderNumber, i.Status,
                (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDateUtc < today,
                i.IssueDateUtc, i.DueDateUtc, i.TotalAmount, i.AmountPaid))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<InvoiceDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), id);

        return new InvoiceDto(
            invoice.Id, invoice.InvoiceNumber, invoice.CustomerId, invoice.Customer.Name, invoice.SalesOrderId,
            invoice.SalesOrder.OrderNumber, invoice.Status, invoice.IsOverdue(DateTime.UtcNow), invoice.IssueDateUtc,
            invoice.DueDateUtc, invoice.TotalAmount, invoice.AmountPaid, invoice.Balance,
            invoice.Lines.Select(l => new InvoiceLineDto(l.Id, l.Description, l.Quantity, l.UnitPrice)).ToList(),
            invoice.Payments
                .OrderBy(p => p.PaidAtUtc)
                .Select(p => new PaymentDto(p.Id, p.Amount, p.PaidAtUtc, p.Method, p.Reference))
                .ToList());
    }

    public async Task<InvoiceDto> CreateFromOrderAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var order = await db.SalesOrders.AsNoTracking()
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == request.SalesOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), request.SalesOrderId);

        var existing = await db.Invoices
            .Where(i => i.SalesOrderId == order.Id && i.Status != InvoiceStatus.Cancelled)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"Order {order.OrderNumber} is already invoiced on {existing}.");
        }

        var number = await DocumentNumbers.NextAsync(db.Invoices.Select(i => i.InvoiceNumber), "INV-", 30001, cancellationToken);
        var invoice = Invoice.FromOrder(order, number);

        db.Invoices.Add(invoice);
        audit.Record("CreateInvoice", "Invoice", invoice.InvoiceNumber, $"Drafted {invoice.InvoiceNumber} for {order.OrderNumber} ({invoice.TotalAmount:N2})");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(invoice.Id, cancellationToken);
    }

    public async Task<InvoiceDto> IssueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadAsync(id, cancellationToken);
        var terms = await db.Customers.Where(c => c.Id == invoice.CustomerId).Select(c => c.PaymentTermsDays).FirstAsync(cancellationToken);

        invoice.Issue(DateTime.UtcNow, terms);
        audit.Record("IssueInvoice", "Invoice", invoice.InvoiceNumber, $"Issued {invoice.InvoiceNumber}, due {invoice.DueDateUtc:yyyy-MM-dd}");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<InvoiceDto> RecordPaymentAsync(Guid id, RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        await paymentValidator.ValidateAndThrowAsync(request, cancellationToken);
        var invoice = await LoadAsync(id, cancellationToken);

        invoice.RecordPayment(request.Amount, request.PaidAtUtc ?? DateTime.UtcNow, request.Method, request.Reference?.Trim());
        audit.Record("RecordPayment", "Invoice", invoice.InvoiceNumber,
            $"Recorded {request.Amount:N2} by {request.Method} against {invoice.InvoiceNumber} ({invoice.Status})");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<InvoiceDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadAsync(id, cancellationToken);
        invoice.Cancel();
        audit.Record("CancelInvoice", "Invoice", invoice.InvoiceNumber, $"Cancelled {invoice.InvoiceNumber}");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<Invoice> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(Invoice), id);
}
