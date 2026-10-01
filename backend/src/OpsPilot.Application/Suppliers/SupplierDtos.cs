namespace OpsPilot.Application.Suppliers;

public record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    int PaymentTermsDays,
    int AverageLeadTimeDays,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record SupplierListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    int AverageLeadTimeDays,
    bool IsActive);

public record SaveSupplierRequest(
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    int PaymentTermsDays,
    int AverageLeadTimeDays,
    bool IsActive);
