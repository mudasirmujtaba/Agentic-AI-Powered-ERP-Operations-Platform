namespace OpsPilot.Domain.Identity;

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string SalesUser = "SalesUser";
    public const string InventoryManager = "InventoryManager";
    public const string ProcurementUser = "ProcurementUser";
    public const string FinanceUser = "FinanceUser";

    public static readonly IReadOnlyList<string> All =
    [
        Administrator,
        Manager,
        SalesUser,
        InventoryManager,
        ProcurementUser,
        FinanceUser
    ];
}
