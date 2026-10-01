using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Inventory;

/// <summary>Stock of one product in one warehouse. Quantities only change through the methods below so the invariants hold.</summary>
public class InventoryItem : BaseEntity
{
    private InventoryItem() { }

    public InventoryItem(Guid productId, Guid warehouseId)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
    }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;

    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    public void Receive(int quantity)
    {
        EnsurePositive(quantity);
        QuantityOnHand += quantity;
    }

    /// <summary>Removes unreserved stock (adjustment, damage, transfer out).</summary>
    public void Remove(int quantity, string productLabel)
    {
        EnsurePositive(quantity);
        if (quantity > QuantityAvailable)
        {
            throw new BusinessRuleException(
                $"Cannot remove {quantity} of {productLabel}: only {QuantityAvailable} available (on hand {QuantityOnHand}, reserved {QuantityReserved}).");
        }
        QuantityOnHand -= quantity;
    }

    public void Reserve(int quantity, string productLabel)
    {
        EnsurePositive(quantity);
        if (quantity > QuantityAvailable)
        {
            throw new BusinessRuleException($"Cannot reserve {quantity} of {productLabel}: only {QuantityAvailable} available.");
        }
        QuantityReserved += quantity;
    }

    public void Release(int quantity)
    {
        EnsurePositive(quantity);
        QuantityReserved = Math.Max(0, QuantityReserved - quantity);
    }

    /// <summary>Ships previously reserved stock: it leaves both the reservation and the shelf.</summary>
    public void Ship(int quantity, string productLabel)
    {
        EnsurePositive(quantity);
        if (quantity > QuantityReserved || quantity > QuantityOnHand)
        {
            throw new BusinessRuleException($"Cannot ship {quantity} of {productLabel}: only {QuantityReserved} reserved.");
        }
        QuantityReserved -= quantity;
        QuantityOnHand -= quantity;
    }

    private static void EnsurePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException("Quantity must be greater than zero.");
        }
    }
}
