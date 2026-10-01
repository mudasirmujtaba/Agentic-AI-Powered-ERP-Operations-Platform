namespace OpsPilot.Domain.Customers;

public class CustomerAddress
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }

    public AddressType Type { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public enum AddressType
{
    Billing,
    Shipping
}
