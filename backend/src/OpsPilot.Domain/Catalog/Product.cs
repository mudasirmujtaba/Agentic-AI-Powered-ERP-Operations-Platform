using OpsPilot.Domain.Common;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Domain.Catalog;

public class Product : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public decimal UnitPrice { get; set; }
    public decimal Cost { get; set; }
    public int ReorderPoint { get; set; }
    public int SafetyStock { get; set; }

    public Guid? PrimarySupplierId { get; set; }
    public Supplier? PrimarySupplier { get; set; }

    public bool IsActive { get; set; } = true;
}
