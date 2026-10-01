import { SalesOrderStatus } from '../../../shared/models/statuses';

export interface SalesOrderListItem {
  id: string;
  orderNumber: string;
  customerName: string;
  warehouseCode: string;
  status: SalesOrderStatus;
  orderDateUtc: string;
  requiredDateUtc: string | null;
  totalAmount: number;
  isLate: boolean;
}

export interface SalesOrderLine {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface SalesOrder {
  id: string;
  orderNumber: string;
  customerId: string;
  customerName: string;
  warehouseId: string;
  warehouseName: string;
  status: SalesOrderStatus;
  orderDateUtc: string;
  requiredDateUtc: string | null;
  confirmedAtUtc: string | null;
  shippedAtUtc: string | null;
  deliveredAtUtc: string | null;
  cancelledAtUtc: string | null;
  carrier: string | null;
  trackingNumber: string | null;
  notes: string | null;
  totalAmount: number;
  lines: SalesOrderLine[];
  invoiceId: string | null;
  invoiceNumber: string | null;
}

export interface SaveSalesOrderRequest {
  customerId: string;
  warehouseId: string;
  requiredDateUtc: string | null;
  notes: string | null;
  lines: { productId: string; quantity: number; unitPrice: number | null }[];
}
