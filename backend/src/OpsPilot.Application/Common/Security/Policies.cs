using OpsPilot.Domain.Identity;

namespace OpsPilot.Application.Common.Security;

/// <summary>Write-access policies derived from the design doc's role permission matrix. Reads only require authentication.</summary>
public static class Policies
{
    public const string ManageCustomers = nameof(ManageCustomers);
    public const string ManageSuppliers = nameof(ManageSuppliers);
    public const string ManageCatalog = nameof(ManageCatalog);
    public const string ManageWarehouses = nameof(ManageWarehouses);
    public const string ManageInventory = nameof(ManageInventory);
    public const string ManageSalesOrders = nameof(ManageSalesOrders);
    public const string FulfilSalesOrders = nameof(FulfilSalesOrders);
    public const string ManagePurchaseOrders = nameof(ManagePurchaseOrders);
    public const string ApprovePurchaseOrders = nameof(ApprovePurchaseOrders);
    public const string ReceiveGoods = nameof(ReceiveGoods);
    public const string ManageFinance = nameof(ManageFinance);
    public const string ViewAuditLog = nameof(ViewAuditLog);
    public const string ManageTickets = nameof(ManageTickets);
    public const string ManageJobs = nameof(ManageJobs);

    public static readonly IReadOnlyDictionary<string, string[]> RolesByPolicy = new Dictionary<string, string[]>
    {
        [ManageCustomers] = [Roles.Administrator, Roles.Manager, Roles.SalesUser],
        [ManageSuppliers] = [Roles.Administrator, Roles.Manager, Roles.ProcurementUser],
        [ManageCatalog] = [Roles.Administrator, Roles.Manager, Roles.InventoryManager],
        [ManageWarehouses] = [Roles.Administrator, Roles.InventoryManager],
        [ManageInventory] = [Roles.Administrator, Roles.Manager, Roles.InventoryManager],
        [ManageSalesOrders] = [Roles.Administrator, Roles.Manager, Roles.SalesUser],
        [FulfilSalesOrders] = [Roles.Administrator, Roles.Manager, Roles.InventoryManager],
        [ManagePurchaseOrders] = [Roles.Administrator, Roles.Manager, Roles.ProcurementUser],
        [ApprovePurchaseOrders] = [Roles.Administrator, Roles.Manager],
        [ReceiveGoods] = [Roles.Administrator, Roles.Manager, Roles.InventoryManager, Roles.ProcurementUser],
        [ManageFinance] = [Roles.Administrator, Roles.Manager, Roles.FinanceUser],
        [ViewAuditLog] = [Roles.Administrator, Roles.Manager],
        [ManageTickets] = [Roles.Administrator, Roles.Manager, Roles.SalesUser, Roles.InventoryManager],
        [ManageJobs] = [Roles.Administrator, Roles.Manager],
    };
}
