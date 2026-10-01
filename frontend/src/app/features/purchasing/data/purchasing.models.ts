import { PurchaseOrderStatus } from '../../../shared/models/statuses';

export interface PurchaseOrderListItem {
  id: string;
  poNumber: string;
  supplierName: string;
  warehouseCode: string;
  status: PurchaseOrderStatus;
  orderDateUtc: string;
  expectedDeliveryDateUtc: string | null;
  totalAmount: number;
}

export interface PurchaseOrderLine {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitCost: number;
  quantityReceived: number;
  quantityRemaining: number;
  lineTotal: number;
}

export interface PurchaseOrder {
  id: string;
  poNumber: string;
  supplierId: string;
  supplierName: string;
  warehouseId: string;
  warehouseName: string;
  status: PurchaseOrderStatus;
  orderDateUtc: string;
  expectedDeliveryDateUtc: string | null;
  submittedAtUtc: string | null;
  approvedAtUtc: string | null;
  orderedAtUtc: string | null;
  completedAtUtc: string | null;
  cancelledAtUtc: string | null;
  rejectionReason: string | null;
  notes: string | null;
  totalAmount: number;
  requiresApproval: boolean;
  approvalThreshold: number;
  lines: PurchaseOrderLine[];
}

export interface SavePurchaseOrderRequest {
  supplierId: string;
  warehouseId: string;
  expectedDeliveryDateUtc: string | null;
  notes: string | null;
  lines: { productId: string; quantity: number; unitCost: number | null }[];
}
