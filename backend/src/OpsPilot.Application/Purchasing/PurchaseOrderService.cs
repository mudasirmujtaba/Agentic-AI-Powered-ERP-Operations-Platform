using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Inventory;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Purchasing;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Application.Purchasing;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderListItemDto>> ListAsync(PurchaseOrderQuery query, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> RejectAsync(Guid id, RejectPurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> MarkOrderedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> ReceiveAsync(Guid id, ReceiveGoodsRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

public class PurchaseOrderService(
    IApplicationDbContext db,
    StockLedger ledger,
    ICurrentUserService currentUser,
    IValidator<SavePurchaseOrderRequest> saveValidator,
    IValidator<RejectPurchaseOrderRequest> rejectValidator,
    IValidator<ReceiveGoodsRequest> receiveValidator) : IPurchaseOrderService
{
    private static readonly Dictionary<string, Expression<Func<PurchaseOrder, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["poNumber"] = o => o.PoNumber,
            ["supplierName"] = o => o.Supplier.Name,
            ["status"] = o => o.Status,
            ["orderDateUtc"] = o => o.OrderDateUtc,
            ["expectedDeliveryDateUtc"] = o => o.ExpectedDeliveryDateUtc!,
            ["totalAmount"] = o => o.TotalAmount,
        };

    public async Task<PagedResult<PurchaseOrderListItemDto>> ListAsync(PurchaseOrderQuery query, CancellationToken cancellationToken = default)
    {
        var orders = db.PurchaseOrders.AsNoTracking();
        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);
        if (query.SupplierId is { } supplierId) orders = orders.Where(o => o.SupplierId == supplierId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            orders = orders.Where(o => o.PoNumber.Contains(search) || o.Supplier.Name.Contains(search));
        }

        return await orders
            .ApplySort(query, SortMap, "poNumber")
            .Select(o => new PurchaseOrderListItemDto(
                o.Id, o.PoNumber, o.Supplier.Name, o.Warehouse.Code, o.Status, o.OrderDateUtc, o.ExpectedDeliveryDateUtc, o.TotalAmount))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var po = await db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Warehouse)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), id);

        return new PurchaseOrderDto(
            po.Id, po.PoNumber, po.SupplierId, po.Supplier.Name, po.WarehouseId, po.Warehouse.Name, po.Status,
            po.OrderDateUtc, po.ExpectedDeliveryDateUtc, po.SubmittedAtUtc, po.ApprovedAtUtc, po.OrderedAtUtc,
            po.CompletedAtUtc, po.CancelledAtUtc, po.RejectionReason, po.Notes, po.TotalAmount,
            po.RequiresApproval, PurchaseOrder.ApprovalThreshold,
            po.Lines
                .OrderBy(l => l.Product.Code)
                .Select(l => new PurchaseOrderLineDto(l.Id, l.ProductId, l.Product.Code, l.Product.Name, l.Quantity, l.UnitCost, l.QuantityReceived))
                .ToList());
    }

    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        await saveValidator.ValidateAndThrowAsync(request, cancellationToken);

        var po = new PurchaseOrder
        {
            PoNumber = await DocumentNumbers.NextAsync(db.PurchaseOrders.Select(o => o.PoNumber), "PO-", 20001, cancellationToken),
            OrderDateUtc = DateTime.UtcNow,
        };
        await ApplyAsync(po, request, cancellationToken);

        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(po.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        await saveValidator.ValidateAndThrowAsync(request, cancellationToken);
        var po = await LoadAsync(id, cancellationToken);
        await ApplyAsync(po, request, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public Task<PurchaseOrderDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, po => po.Submit(DateTime.UtcNow), cancellationToken);

    public Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, po => po.Approve(currentUser.UserId, DateTime.UtcNow), cancellationToken);

    public async Task<PurchaseOrderDto> RejectAsync(Guid id, RejectPurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        await rejectValidator.ValidateAndThrowAsync(request, cancellationToken);
        return await TransitionAsync(id, po => po.Reject(request.Reason.Trim()), cancellationToken);
    }

    public Task<PurchaseOrderDto> MarkOrderedAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, po => po.MarkOrdered(DateTime.UtcNow), cancellationToken);

    public Task<PurchaseOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, po => po.Cancel(DateTime.UtcNow), cancellationToken);

    public async Task<PurchaseOrderDto> ReceiveAsync(Guid id, ReceiveGoodsRequest request, CancellationToken cancellationToken = default)
    {
        await receiveValidator.ValidateAndThrowAsync(request, cancellationToken);
        var po = await LoadAsync(id, cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var receipt in request.Lines.Where(l => l.Quantity > 0))
        {
            var line = po.Lines.FirstOrDefault(l => l.Id == receipt.LineId)
                ?? throw new NotFoundException(nameof(PurchaseOrderLine), receipt.LineId);

            po.Receive(line, receipt.Quantity, now);
            await ledger.AddAsync(line.ProductId, po.WarehouseId, receipt.Quantity, InventoryTransactionType.Purchase,
                po.PoNumber, null, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<PurchaseOrderDto> TransitionAsync(Guid id, Action<PurchaseOrder> transition, CancellationToken cancellationToken)
    {
        var po = await LoadAsync(id, cancellationToken);
        transition(po);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<PurchaseOrder> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.PurchaseOrders
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(PurchaseOrder), id);

    private async Task ApplyAsync(PurchaseOrder po, SavePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.SupplierId);
        if (!supplier.IsActive)
        {
            throw new BusinessRuleException($"Supplier {supplier.Name} is inactive.");
        }

        var warehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.WarehouseId);
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

        po.SupplierId = supplier.Id;
        po.WarehouseId = warehouse.Id;
        po.ExpectedDeliveryDateUtc = request.ExpectedDeliveryDateUtc ?? DateTime.UtcNow.Date.AddDays(supplier.AverageLeadTimeDays);
        po.Notes = request.Notes?.Trim();
        po.SetLines(request.Lines.Select(l => new PurchaseOrderLine
        {
            ProductId = l.ProductId,
            Product = products[l.ProductId],
            Quantity = l.Quantity,
            UnitCost = l.UnitCost ?? products[l.ProductId].Cost,
        }));
    }
}
