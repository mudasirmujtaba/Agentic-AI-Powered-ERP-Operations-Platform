using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Inventory;

namespace OpsPilot.Application.Inventory;

public interface IInventoryService
{
    Task<PagedResult<StockLevelDto>> ListStockAsync(StockQuery query, CancellationToken cancellationToken = default);
    Task<ProductStockDto> GetProductStockAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<PagedResult<InventoryTransactionDto>> ListTransactionsAsync(InventoryTransactionQuery query, CancellationToken cancellationToken = default);
    Task<ProductStockDto> AdjustAsync(AdjustStockRequest request, CancellationToken cancellationToken = default);
    Task<ProductStockDto> TransferAsync(TransferStockRequest request, CancellationToken cancellationToken = default);
}

public class InventoryService(
    IApplicationDbContext db,
    StockLedger ledger,
    AuditLogWriter audit,
    IValidator<AdjustStockRequest> adjustValidator,
    IValidator<TransferStockRequest> transferValidator) : IInventoryService
{
    private static readonly Dictionary<string, Expression<Func<StockLevelDto, object>>> StockSortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["productCode"] = s => s.ProductCode,
            ["productName"] = s => s.ProductName,
            ["categoryName"] = s => s.CategoryName,
            ["quantityOnHand"] = s => s.QuantityOnHand,
            ["quantityAvailable"] = s => s.QuantityAvailable,
            ["reorderPoint"] = s => s.ReorderPoint,
        };

    public async Task<PagedResult<StockLevelDto>> ListStockAsync(StockQuery query, CancellationToken cancellationToken = default)
    {
        var items = db.InventoryItems.AsNoTracking();
        if (query.WarehouseId is { } warehouseId)
        {
            items = items.Where(i => i.WarehouseId == warehouseId);
        }

        var stock = db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new StockLevelDto
            {
                ProductId = p.Id,
                ProductCode = p.Code,
                ProductName = p.Name,
                CategoryName = p.Category.Name,
                QuantityOnHand = items.Where(i => i.ProductId == p.Id).Sum(i => (int?)i.QuantityOnHand) ?? 0,
                QuantityReserved = items.Where(i => i.ProductId == p.Id).Sum(i => (int?)i.QuantityReserved) ?? 0,
                QuantityAvailable = items.Where(i => i.ProductId == p.Id).Sum(i => (int?)(i.QuantityOnHand - i.QuantityReserved)) ?? 0,
                ReorderPoint = p.ReorderPoint,
                SafetyStock = p.SafetyStock,
            });

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            stock = stock.Where(s => s.ProductCode.Contains(search) || s.ProductName.Contains(search));
        }

        if (query.LowStockOnly)
        {
            stock = stock.Where(s => s.QuantityAvailable <= s.ReorderPoint);
        }

        return await stock.ApplySort(query, StockSortMap, "productCode").ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<ProductStockDto> GetProductStockAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), productId);

        var warehouses = await db.InventoryItems.AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.Warehouse.Code)
            .Select(i => new WarehouseStockDto(
                i.WarehouseId, i.Warehouse.Code, i.Warehouse.Name,
                i.QuantityOnHand, i.QuantityReserved, i.QuantityOnHand - i.QuantityReserved))
            .ToListAsync(cancellationToken);

        var recent = await FilteredTransactions(new InventoryTransactionQuery { ProductId = productId })
            .OrderByDescending(t => t.OccurredAtUtc)
            .Take(15)
            .Select(ToTransactionDto)
            .ToListAsync(cancellationToken);

        var onHand = warehouses.Sum(w => w.QuantityOnHand);
        var reserved = warehouses.Sum(w => w.QuantityReserved);
        var available = onHand - reserved;

        return new ProductStockDto(
            product.Id, product.Code, product.Name, product.ReorderPoint, product.SafetyStock,
            onHand, reserved, available,
            StockStatusRules.For(available, product.SafetyStock, product.ReorderPoint),
            warehouses, recent);
    }

    public async Task<PagedResult<InventoryTransactionDto>> ListTransactionsAsync(InventoryTransactionQuery query, CancellationToken cancellationToken = default)
    {
        return await FilteredTransactions(query)
            .OrderByDescending(t => t.OccurredAtUtc)
            .Select(ToTransactionDto)
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<ProductStockDto> AdjustAsync(AdjustStockRequest request, CancellationToken cancellationToken = default)
    {
        await adjustValidator.ValidateAndThrowAsync(request, cancellationToken);
        var product = await RequireProductAsync(request.ProductId, cancellationToken);
        await RequireWarehouseAsync(request.WarehouseId, cancellationToken);

        if (request.Quantity > 0)
        {
            await ledger.AddAsync(request.ProductId, request.WarehouseId, request.Quantity, request.Reason, "Manual", request.Notes, cancellationToken);
        }
        else
        {
            await ledger.RemoveAsync(request.ProductId, request.WarehouseId, -request.Quantity, request.Reason, product.Code, "Manual", request.Notes, cancellationToken);
        }

        audit.Record("AdjustStock", "Product", product.Code,
            $"{request.Reason}: {(request.Quantity > 0 ? "+" : "")}{request.Quantity} {product.Code}{(request.Notes is null ? "" : $" ({request.Notes})")}");
        await db.SaveChangesAsync(cancellationToken);
        return await GetProductStockAsync(request.ProductId, cancellationToken);
    }

    public async Task<ProductStockDto> TransferAsync(TransferStockRequest request, CancellationToken cancellationToken = default)
    {
        await transferValidator.ValidateAndThrowAsync(request, cancellationToken);
        var product = await RequireProductAsync(request.ProductId, cancellationToken);
        var from = await RequireWarehouseAsync(request.FromWarehouseId, cancellationToken);
        var to = await RequireWarehouseAsync(request.ToWarehouseId, cancellationToken);

        var reference = $"{from.Code} → {to.Code}";
        await ledger.RemoveAsync(product.Id, from.Id, request.Quantity, InventoryTransactionType.Transfer, product.Code, reference, request.Notes, cancellationToken);
        await ledger.AddAsync(product.Id, to.Id, request.Quantity, InventoryTransactionType.Transfer, reference, request.Notes, cancellationToken);

        audit.Record("TransferStock", "Product", product.Code, $"Moved {request.Quantity} {product.Code} {reference}");
        await db.SaveChangesAsync(cancellationToken);
        return await GetProductStockAsync(request.ProductId, cancellationToken);
    }

    // Sort before projecting: EF can't translate ordering on members of a positional record.
    private static readonly Expression<Func<InventoryTransaction, InventoryTransactionDto>> ToTransactionDto = t =>
        new InventoryTransactionDto(t.Id, t.OccurredAtUtc, t.Type, t.Quantity, t.Product.Code, t.Product.Name, t.Warehouse.Code, t.Reference, t.Notes);

    private IQueryable<InventoryTransaction> FilteredTransactions(InventoryTransactionQuery query)
    {
        var transactions = db.InventoryTransactions.AsNoTracking();
        if (query.ProductId is { } productId) transactions = transactions.Where(t => t.ProductId == productId);
        if (query.WarehouseId is { } warehouseId) transactions = transactions.Where(t => t.WarehouseId == warehouseId);
        if (query.Type is { } type) transactions = transactions.Where(t => t.Type == type);
        return transactions;
    }

    private async Task<Product> RequireProductAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(Product), id);

    private async Task<Warehouse> RequireWarehouseAsync(Guid id, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), id);
        if (!warehouse.IsActive)
        {
            throw new Domain.Common.BusinessRuleException($"Warehouse {warehouse.Code} is inactive.");
        }
        return warehouse;
    }
}
