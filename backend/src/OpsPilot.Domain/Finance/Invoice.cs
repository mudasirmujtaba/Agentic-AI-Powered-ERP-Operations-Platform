using OpsPilot.Domain.Common;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Sales;

namespace OpsPilot.Domain.Finance;

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;
    public DateTime? IssueDateUtc { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal Balance => TotalAmount - AmountPaid;

    public ICollection<InvoiceLine> Lines { get; private set; } = new List<InvoiceLine>();
    public ICollection<Payment> Payments { get; private set; } = new List<Payment>();

    /// <summary>Unpaid money the customer owes (counts toward credit exposure).</summary>
    public bool IsOutstanding => Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid;

    public bool IsOverdue(DateTime nowUtc) => IsOutstanding && DueDateUtc < nowUtc.Date;

    /// <summary>Whole days past the due date (0 when not overdue).</summary>
    public int DaysOverdue(DateTime nowUtc) =>
        IsOverdue(nowUtc) && DueDateUtc is { } due ? (nowUtc.Date - due.Date).Days : 0;

    /// <summary>Highest overdue-reminder stage already sent (0, 7, 14 or 30 days), so reminders are never repeated.</summary>
    public int ReminderStage { get; private set; }

    public void MarkReminderSent(int stage)
    {
        if (stage <= ReminderStage)
        {
            throw new BusinessRuleException($"Reminder stage {stage} was already sent for {InvoiceNumber}.");
        }
        ReminderStage = stage;
    }

    public static Invoice FromOrder(SalesOrder order, string invoiceNumber)
    {
        if (order.Status is not (SalesOrderStatus.Shipped or SalesOrderStatus.Delivered))
        {
            throw new BusinessRuleException($"Order {order.OrderNumber} must be shipped before it can be invoiced.");
        }

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            CustomerId = order.CustomerId,
            SalesOrderId = order.Id,
        };

        foreach (var line in order.Lines)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                ProductId = line.ProductId,
                Description = line.Product is { } product ? $"{product.Code} {product.Name}" : "Item",
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
            });
        }

        invoice.TotalAmount = invoice.Lines.Sum(l => l.Quantity * l.UnitPrice);
        return invoice;
    }

    public void Issue(DateTime nowUtc, int paymentTermsDays)
    {
        EnsureStatus("issue", InvoiceStatus.Draft);
        Status = InvoiceStatus.Issued;
        IssueDateUtc = nowUtc;
        DueDateUtc = nowUtc.Date.AddDays(paymentTermsDays);
    }

    public Payment RecordPayment(decimal amount, DateTime paidAtUtc, PaymentMethod method, string? reference)
    {
        EnsureStatus("record a payment on", InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid);
        if (amount <= 0 || amount > Balance)
        {
            throw new BusinessRuleException($"Payment must be between 0.01 and the outstanding balance of {Balance:N2}.");
        }

        var payment = new Payment { Amount = amount, PaidAtUtc = paidAtUtc, Method = method, Reference = reference };
        Payments.Add(payment);
        AmountPaid += amount;
        Status = AmountPaid >= TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        return payment;
    }

    public void Cancel()
    {
        EnsureStatus("cancel", InvoiceStatus.Draft, InvoiceStatus.Issued);
        if (AmountPaid > 0)
        {
            throw new BusinessRuleException("An invoice with payments cannot be cancelled.");
        }
        Status = InvoiceStatus.Cancelled;
    }

    private void EnsureStatus(string action, params InvoiceStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new BusinessRuleException(
                $"Cannot {action} invoice {InvoiceNumber} while it is {Status}; allowed only when {string.Join(" or ", allowed)}.");
        }
    }
}

public class InvoiceLine
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class Payment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAtUtc { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
}

public enum InvoiceStatus
{
    Draft,
    Issued,
    PartiallyPaid,
    Paid,
    Cancelled
}

public enum PaymentMethod
{
    BankTransfer,
    Card,
    Check,
    Cash
}
