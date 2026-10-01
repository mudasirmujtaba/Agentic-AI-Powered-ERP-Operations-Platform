namespace OpsPilot.Domain.Identity;

/// <summary>Non-interactive accounts the platform itself uses.</summary>
public static class SystemAccounts
{
    /// <summary>Identity background jobs act as when calling the AI service. Has no password, so it cannot sign in.</summary>
    public const string AutomationEmail = "automation@opspilot.local";
}

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
