namespace OpsPilot.Application.Warehouses;

public record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string? Location,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record SaveWarehouseRequest(string Code, string Name, string? Location, bool IsActive);
