using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Inventory;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Sales;

namespace OpsPilot.Application.Sales;

public interface ISalesOrderService
{
    Task<PagedResult<SalesOrderListItemDto>> ListAsync(SalesOrderQuery query, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> CreateAsync(SaveSalesOrderRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> UpdateAsync(Guid id, SaveSalesOrderRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> ConfirmAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> StartProcessingAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> ShipAsync(Guid id, ShipSalesOrderRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> DeliverAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

public class SalesOrderService(
    IApplicationDbContext db,
    StockLedger ledger,
    IValidator<SaveSalesOrderRequest> validator) : ISalesOrderService
{
    private static readonly Dictionary<string, Expression<Func<SalesOrder, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["orderNumber"] = o => o.OrderNumber,
            ["customerName"] = o => o.Customer.Name,
            ["status"] = o => o.Status,
            ["orderDateUtc"] = o => o.OrderDateUtc,
            ["requiredDateUtc"] = o => o.RequiredDateUtc!,
            ["totalAmount"] = o => o.TotalAmount,
        };

    public async Task<PagedResult<SalesOrderListItemDto>> ListAsync(SalesOrderQuery query, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var orders = db.SalesOrders.AsNoTracking();

        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);
        if (query.CustomerId is { } customerId) orders = orders.Where(o => o.CustomerId == customerId);
        if (query.LateOnly) orders = orders.Where(IsLate(today));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            orders = orders.Where(o => o.OrderNumber.Contains(search) || o.Customer.Name.Contains(search));
        }

        return await orders
            .ApplySort(query, SortMap, "orderNumber")
            .Select(o => new SalesOrderListItemDto(
                o.Id, o.OrderNumber, o.Customer.Name, o.Warehouse.Code, o.Status, o.OrderDateUtc, o.RequiredDateUtc, o.TotalAmount,
                (o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing) && o.RequiredDateUtc < today))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public static Expression<Func<SalesOrder, bool>> IsLate(DateTime today) =>
        o => (o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing) && o.RequiredDateUtc < today;

    public async Task<SalesOrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await db.SalesOrders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Warehouse)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), id);

        var invoice = await db.Invoices.AsNoTracking()
            .Where(i => i.SalesOrderId == id && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.Id, i.InvoiceNumber })
            .FirstOrDefaultAsync(cancellationToken);

        return new SalesOrderDto(
            order.Id, order.OrderNumber, order.CustomerId, order.Customer.Name, order.WarehouseId, order.Warehouse.Name,
            order.Status, order.OrderDateUtc, order.RequiredDateUtc, order.ConfirmedAtUtc, order.ShippedAtUtc,
            order.DeliveredAtUtc, order.CancelledAtUtc, order.Carrier, order.TrackingNumber, order.Notes, order.TotalAmount,
            order.Lines
                .OrderBy(l => l.Product.Code)
                .Select(l => new SalesOrderLineDto(l.Id, l.ProductId, l.Product.Code, l.Product.Name, l.Quantity, l.UnitPrice))
                .ToList(),
            invoice?.Id,
            invoice?.InvoiceNumber);
    }

    public async Task<SalesOrderDto> CreateAsync(SaveSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var order = new SalesOrder
        {
            OrderNumber = await DocumentNumbers.NextAsync(db.SalesOrders.Select(o => o.OrderNumber), "SO-", 10001, cancellationToken),
            OrderDateUtc = DateTime.UtcNow,
        };
        await ApplyAsync(order, request, cancellationToken);

        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(order.Id, cancellationToken);
    }

    public async Task<SalesOrderDto> UpdateAsync(Guid id, SaveSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var order = await LoadAsync(id, cancellationToken);

        await ApplyAsync(order, request, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SalesOrderDto> ConfirmAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == order.CustomerId, cancellationToken);

        if (customer.Status != CustomerStatus.Active)
        {
            throw new BusinessRuleException($"{customer.Name} is {customer.Status}; orders can only be confirmed for active customers.");
        }

        await EnsureWithinCreditLimitAsync(order, customer, cancellationToken);
        await EnsureStockAvailableAsync(order, cancellationToken);

        foreach (var line in order.Lines)
        {
            await ledger.ReserveAsync(line.ProductId, order.WarehouseId, line.Quantity, line.Product.Code, cancellationToken);
        }

        order.Confirm(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SalesOrderDto> StartProcessingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        order.StartProcessing();
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SalesOrderDto> ShipAsync(Guid id, ShipSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        order.Ship(DateTime.UtcNow, request.Carrier?.Trim(), request.TrackingNumber?.Trim());

        foreach (var line in order.Lines)
        {
            await ledger.ShipAsync(line.ProductId, order.WarehouseId, line.Quantity, line.Product.Code, order.OrderNumber, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SalesOrderDto> DeliverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        order.Deliver(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SalesOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        var releaseReservations = order.HoldsReservation;

        order.Cancel(DateTime.UtcNow);

        if (releaseReservations)
        {
            foreach (var line in order.Lines)
            {
                await ledger.ReleaseAsync(line.ProductId, order.WarehouseId, line.Quantity, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<SalesOrder> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.SalesOrders
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(SalesOrder), id);

    private async Task ApplyAsync(SalesOrder order, SaveSalesOrderRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);
        if (customer.Status == CustomerStatus.Inactive)
        {
            throw new BusinessRuleException($"{customer.Name} is inactive and cannot place orders.");
        }

        var warehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse", request.WarehouseId);
        if (!warehouse.IsActive)
        {
            throw new BusinessRuleException($"Warehouse {warehouse.Code} is inactive.");
        }

        var productIds = request.Lines.Select(l => l.ProductId).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var missing = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException("Product", string.Join(", ", missing));
        }

        var inactive = products.Values.Where(p => !p.IsActive).Select(p => p.Code).ToList();
        if (inactive.Count > 0)
        {
            throw new BusinessRuleException($"Inactive products cannot be ordered: {string.Join(", ", inactive)}.");
        }

        order.CustomerId = customer.Id;
        order.WarehouseId = warehouse.Id;
        order.RequiredDateUtc = request.RequiredDateUtc;
        order.Notes = request.Notes?.Trim();
        order.SetLines(request.Lines.Select(l => new SalesOrderLine
        {
            ProductId = l.ProductId,
            Product = products[l.ProductId],
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice ?? products[l.ProductId].UnitPrice,
        }));
    }

    /// <summary>Exposure = unpaid invoice balances + open orders not yet invoiced. Amounts are summed in memory to stay provider-neutral.</summary>
    private async Task EnsureWithinCreditLimitAsync(SalesOrder order, Customer customer, CancellationToken cancellationToken)
    {
        var unpaidInvoices = await db.Invoices.AsNoTracking()
            .Where(i => i.CustomerId == customer.Id && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid))
            .Select(i => new { i.TotalAmount, i.AmountPaid })
            .ToListAsync(cancellationToken);

        var uninvoicedOrders = await db.SalesOrders.AsNoTracking()
            .Where(o => o.CustomerId == customer.Id && o.Id != order.Id &&
                        (o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing ||
                         o.Status == SalesOrderStatus.Shipped || o.Status == SalesOrderStatus.Delivered) &&
                        !db.Invoices.Any(i => i.SalesOrderId == o.Id && i.Status != InvoiceStatus.Cancelled))
            .Select(o => o.TotalAmount)
            .ToListAsync(cancellationToken);

        var exposure = unpaidInvoices.Sum(i => i.TotalAmount - i.AmountPaid) + uninvoicedOrders.Sum();
        if (exposure + order.TotalAmount > customer.CreditLimit)
        {
            throw new BusinessRuleException(
                $"Confirming this order would put {customer.Name} over their credit limit: " +
                $"existing exposure {exposure:N2} + order {order.TotalAmount:N2} > limit {customer.CreditLimit:N2}.");
        }
    }

    private async Task EnsureStockAvailableAsync(SalesOrder order, CancellationToken cancellationToken)
    {
        var productIds = order.Lines.Select(l => l.ProductId).ToList();
        var available = await db.InventoryItems.AsNoTracking()
            .Where(i => i.WarehouseId == order.WarehouseId && productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, i => i.QuantityOnHand - i.QuantityReserved, cancellationToken);

        var shortages = order.Lines
            .Select(l => (l.Product.Code, Needed: l.Quantity, Available: available.GetValueOrDefault(l.ProductId)))
            .Where(s => s.Needed > s.Available)
            .Select(s => $"{s.Code} (need {s.Needed}, available {s.Available})")
            .ToList();

        if (shortages.Count > 0)
        {
            throw new BusinessRuleException($"Not enough stock in the fulfilment warehouse: {string.Join("; ", shortages)}.");
        }
    }
}
