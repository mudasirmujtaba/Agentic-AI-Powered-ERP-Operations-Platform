import { BadgeTone } from '../components/status-badge/status-badge';

export interface StatusStyle {
  label: string;
  tone: BadgeTone;
}

export type SalesOrderStatus = 'Draft' | 'Confirmed' | 'Processing' | 'Shipped' | 'Delivered' | 'Cancelled';
export type PurchaseOrderStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Ordered'
  | 'PartiallyReceived'
  | 'Completed'
  | 'Cancelled';
export type InvoiceStatus = 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid' | 'Cancelled';
export type StockStatus = 'Ok' | 'Reorder' | 'BelowSafetyStock' | 'OutOfStock';

export const SALES_ORDER_STATUSES: Record<SalesOrderStatus, StatusStyle> = {
  Draft: { label: 'Draft', tone: 'neutral' },
  Confirmed: { label: 'Confirmed', tone: 'warning' },
  Processing: { label: 'Processing', tone: 'warning' },
  Shipped: { label: 'Shipped', tone: 'success' },
  Delivered: { label: 'Delivered', tone: 'success' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
};

export const PURCHASE_ORDER_STATUSES: Record<PurchaseOrderStatus, StatusStyle> = {
  Draft: { label: 'Draft', tone: 'neutral' },
  PendingApproval: { label: 'Pending approval', tone: 'warning' },
  Approved: { label: 'Approved', tone: 'success' },
  Ordered: { label: 'Ordered', tone: 'success' },
  PartiallyReceived: { label: 'Partially received', tone: 'warning' },
  Completed: { label: 'Completed', tone: 'success' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
};

export const INVOICE_STATUSES: Record<InvoiceStatus, StatusStyle> = {
  Draft: { label: 'Draft', tone: 'neutral' },
  Issued: { label: 'Issued', tone: 'warning' },
  PartiallyPaid: { label: 'Partially paid', tone: 'warning' },
  Paid: { label: 'Paid', tone: 'success' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
};

export const OVERDUE_STATUS: StatusStyle = { label: 'Overdue', tone: 'danger' };

export const STOCK_STATUSES: Record<StockStatus, StatusStyle> = {
  Ok: { label: 'In stock', tone: 'success' },
  Reorder: { label: 'Reorder', tone: 'warning' },
  BelowSafetyStock: { label: 'Below safety stock', tone: 'danger' },
  OutOfStock: { label: 'Out of stock', tone: 'danger' },
};

/** `{ value, label }` options for status filter selects. */
export function statusOptions<T extends string>(styles: Record<T, StatusStyle>): { value: T; label: string }[] {
  return (Object.keys(styles) as T[]).map((value) => ({ value, label: styles[value].label }));
}
