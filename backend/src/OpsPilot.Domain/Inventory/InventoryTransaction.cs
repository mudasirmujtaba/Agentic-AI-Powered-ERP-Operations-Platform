using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Inventory;

/// <summary>Immutable ledger entry. <see cref="Quantity"/> is signed: positive adds stock, negative removes it.</summary>
public class InventoryTransaction : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public InventoryTransactionType Type { get; set; }
    public int Quantity { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public enum InventoryTransactionType
{
    Purchase,
    Sale,
    Return,
    Adjustment,
    Transfer,
    Damaged
}
