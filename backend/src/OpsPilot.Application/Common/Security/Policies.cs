using OpsPilot.Domain.Identity;

namespace OpsPilot.Application.Common.Security;

/// <summary>Write-access policies derived from the design doc's role permission matrix. Reads only require authentication.</summary>
public static class Policies
{
    public const string ManageCustomers = nameof(ManageCustomers);
    public const string ManageSuppliers = nameof(ManageSuppliers);
    public const string ManageCatalog = nameof(ManageCatalog);
    public const string ManageWarehouses = nameof(ManageWarehouses);

    public static readonly IReadOnlyDictionary<string, string[]> RolesByPolicy = new Dictionary<string, string[]>
    {
        [ManageCustomers] = [Roles.Administrator, Roles.Manager, Roles.SalesUser],
        [ManageSuppliers] = [Roles.Administrator, Roles.Manager, Roles.ProcurementUser],
        [ManageCatalog] = [Roles.Administrator, Roles.Manager, Roles.InventoryManager],
        [ManageWarehouses] = [Roles.Administrator, Roles.InventoryManager],
    };
}
