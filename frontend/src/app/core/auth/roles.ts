export const Roles = {
  Administrator: 'Administrator',
  Manager: 'Manager',
  SalesUser: 'SalesUser',
  InventoryManager: 'InventoryManager',
  ProcurementUser: 'ProcurementUser',
  FinanceUser: 'FinanceUser',
} as const;

const { Administrator, Manager, SalesUser, InventoryManager, ProcurementUser, FinanceUser } = Roles;

/** Mirrors the backend write policies in `OpsPilot.Application.Common.Security.Policies`. UX only; the API enforces access. */
export const WriteAccess = {
  customers: [Administrator, Manager, SalesUser],
  suppliers: [Administrator, Manager, ProcurementUser],
  catalog: [Administrator, Manager, InventoryManager],
  warehouses: [Administrator, InventoryManager],
  inventory: [Administrator, Manager, InventoryManager],
  salesOrders: [Administrator, Manager, SalesUser],
  fulfilment: [Administrator, Manager, InventoryManager],
  purchaseOrders: [Administrator, Manager, ProcurementUser],
  approvePurchaseOrders: [Administrator, Manager],
  receiveGoods: [Administrator, Manager, InventoryManager, ProcurementUser],
  finance: [Administrator, Manager, FinanceUser],
} as const;
