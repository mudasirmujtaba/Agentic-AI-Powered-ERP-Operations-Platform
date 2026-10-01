using OpsPilot.Application.Common.Models;

namespace OpsPilot.Application.Products;

public record ProductDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    decimal UnitPrice,
    decimal Cost,
    int ReorderPoint,
    int SafetyStock,
    Guid? PrimarySupplierId,
    string? PrimarySupplierName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record ProductListItemDto(
    Guid Id,
    string Code,
    string Name,
    string CategoryName,
    decimal UnitPrice,
    int ReorderPoint,
    int SafetyStock,
    string? PrimarySupplierName,
    bool IsActive);

public record SaveProductRequest(
    string Code,
    string Name,
    string? Description,
    Guid CategoryId,
    decimal UnitPrice,
    decimal Cost,
    int ReorderPoint,
    int SafetyStock,
    Guid? PrimarySupplierId,
    bool IsActive);

public class ProductQuery : PagedQuery
{
    public Guid? CategoryId { get; set; }
}
