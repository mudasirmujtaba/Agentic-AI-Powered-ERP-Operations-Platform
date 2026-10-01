export const Roles = {
  Administrator: 'Administrator',
  Manager: 'Manager',
  SalesUser: 'SalesUser',
  InventoryManager: 'InventoryManager',
  ProcurementUser: 'ProcurementUser',
  FinanceUser: 'FinanceUser',
} as const;

/** Mirrors the backend write policies in `OpsPilot.Application.Common.Security.Policies`. UX only; the API enforces access. */
export const WriteAccess = {
  customers: [Roles.Administrator, Roles.Manager, Roles.SalesUser],
  suppliers: [Roles.Administrator, Roles.Manager, Roles.ProcurementUser],
  catalog: [Roles.Administrator, Roles.Manager, Roles.InventoryManager],
  warehouses: [Roles.Administrator, Roles.InventoryManager],
} as const;
