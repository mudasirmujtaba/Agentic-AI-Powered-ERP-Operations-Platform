using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Suppliers;

public class Supplier : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public int AverageLeadTimeDays { get; set; }
    public bool IsActive { get; set; } = true;
}
