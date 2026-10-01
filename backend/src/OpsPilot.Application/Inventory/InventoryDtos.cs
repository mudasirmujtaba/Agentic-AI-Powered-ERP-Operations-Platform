using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Inventory;

namespace OpsPilot.Application.Inventory;

/// <summary>Member-initialised (not positional) so EF can filter and sort on its properties after projection.</summary>
public class StockLevelDto
{
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public int QuantityOnHand { get; init; }
    public int QuantityReserved { get; init; }
    public int QuantityAvailable { get; init; }
    public int ReorderPoint { get; init; }
    public int SafetyStock { get; init; }

    public StockStatus Status => StockStatusRules.For(QuantityAvailable, SafetyStock, ReorderPoint);
}

public enum StockStatus
{
    Ok,
    Reorder,
    BelowSafetyStock,
    OutOfStock
}

public static class StockStatusRules
{
    public static StockStatus For(int available, int safetyStock, int reorderPoint) => available switch
    {
        <= 0 => StockStatus.OutOfStock,
        _ when available <= safetyStock => StockStatus.BelowSafetyStock,
        _ when available <= reorderPoint => StockStatus.Reorder,
        _ => StockStatus.Ok,
    };
}

public class StockQuery : PagedQuery
{
    public Guid? WarehouseId { get; set; }
    public bool LowStockOnly { get; set; }
}

public record WarehouseStockDto(Guid WarehouseId, string WarehouseCode, string WarehouseName, int QuantityOnHand, int QuantityReserved, int QuantityAvailable);

public record ProductStockDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    int ReorderPoint,
    int SafetyStock,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    StockStatus Status,
    IReadOnlyList<WarehouseStockDto> Warehouses,
    IReadOnlyList<InventoryTransactionDto> RecentTransactions);

public record InventoryTransactionDto(
    Guid Id,
    DateTime OccurredAtUtc,
    InventoryTransactionType Type,
    int Quantity,
    string ProductCode,
    string ProductName,
    string WarehouseCode,
    string? Reference,
    string? Notes);

public class InventoryTransactionQuery : PagedQuery
{
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public InventoryTransactionType? Type { get; set; }
}

/// <summary>Quantity is signed: positive adds stock, negative removes it.</summary>
public record AdjustStockRequest(Guid ProductId, Guid WarehouseId, int Quantity, InventoryTransactionType Reason, string? Notes);

public record TransferStockRequest(Guid ProductId, Guid FromWarehouseId, Guid ToWarehouseId, int Quantity, string? Notes);
