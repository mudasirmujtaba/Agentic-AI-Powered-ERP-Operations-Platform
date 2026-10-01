using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Inventory;

namespace OpsPilot.Application.Inventory;

/// <summary>
/// The only way stock quantities change. Every movement also writes a ledger entry; reservations don't,
/// because nothing physically moves. Callers own SaveChanges so a document and its stock effects commit together.
/// </summary>
public class StockLedger(IApplicationDbContext db)
{
    public async Task<InventoryItem> GetOrCreateItemAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken)
    {
        // Check items added earlier in this unit of work first; they are not in the database yet.
        var item = db.InventoryItems.Local.FirstOrDefault(i => i.ProductId == productId && i.WarehouseId == warehouseId)
                   ?? await db.InventoryItems.FirstOrDefaultAsync(
                       i => i.ProductId == productId && i.WarehouseId == warehouseId, cancellationToken);

        if (item is null)
        {
            item = new InventoryItem(productId, warehouseId);
            db.InventoryItems.Add(item);
        }

        return item;
    }

    public async Task AddAsync(Guid productId, Guid warehouseId, int quantity, InventoryTransactionType type,
        string? reference, string? notes, CancellationToken cancellationToken)
    {
        var item = await GetOrCreateItemAsync(productId, warehouseId, cancellationToken);
        item.Receive(quantity);
        Record(productId, warehouseId, type, quantity, reference, notes);
    }

    public async Task RemoveAsync(Guid productId, Guid warehouseId, int quantity, InventoryTransactionType type,
        string productLabel, string? reference, string? notes, CancellationToken cancellationToken)
    {
        var item = await GetOrCreateItemAsync(productId, warehouseId, cancellationToken);
        item.Remove(quantity, productLabel);
        Record(productId, warehouseId, type, -quantity, reference, notes);
    }

    public async Task ReserveAsync(Guid productId, Guid warehouseId, int quantity, string productLabel, CancellationToken cancellationToken)
    {
        var item = await GetOrCreateItemAsync(productId, warehouseId, cancellationToken);
        item.Reserve(quantity, productLabel);
    }

    public async Task ReleaseAsync(Guid productId, Guid warehouseId, int quantity, CancellationToken cancellationToken)
    {
        var item = await GetOrCreateItemAsync(productId, warehouseId, cancellationToken);
        item.Release(quantity);
    }

    public async Task ShipAsync(Guid productId, Guid warehouseId, int quantity, string productLabel, string reference, CancellationToken cancellationToken)
    {
        var item = await GetOrCreateItemAsync(productId, warehouseId, cancellationToken);
        item.Ship(quantity, productLabel);
        Record(productId, warehouseId, InventoryTransactionType.Sale, -quantity, reference, null);
    }

    private void Record(Guid productId, Guid warehouseId, InventoryTransactionType type, int signedQuantity, string? reference, string? notes)
    {
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = signedQuantity,
            Reference = reference,
            Notes = notes,
            OccurredAtUtc = DateTime.UtcNow,
        });
    }
}
