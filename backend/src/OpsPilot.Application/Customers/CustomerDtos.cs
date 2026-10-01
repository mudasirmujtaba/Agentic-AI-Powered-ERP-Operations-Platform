using OpsPilot.Domain.Customers;

namespace OpsPilot.Application.Customers;

public record CustomerAddressDto(
    Guid? Id,
    AddressType Type,
    string Line1,
    string? Line2,
    string City,
    string? State,
    string? PostalCode,
    string Country,
    bool IsDefault);

public record CustomerDto(
    Guid Id,
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    decimal CreditLimit,
    int PaymentTermsDays,
    CustomerStatus Status,
    IReadOnlyList<CustomerAddressDto> Addresses,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record CustomerListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    decimal CreditLimit,
    CustomerStatus Status);

public record SaveCustomerRequest(
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    decimal CreditLimit,
    int PaymentTermsDays,
    CustomerStatus Status,
    IReadOnlyList<CustomerAddressDto> Addresses);
