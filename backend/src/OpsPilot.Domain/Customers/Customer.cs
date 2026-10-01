using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Customers;

public class Customer : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
}

public enum CustomerStatus
{
    Active,
    OnHold,
    Inactive
}
