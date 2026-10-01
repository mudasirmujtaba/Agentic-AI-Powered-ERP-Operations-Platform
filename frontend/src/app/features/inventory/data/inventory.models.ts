import { StockStatus } from '../../../shared/models/statuses';

export type InventoryTransactionType = 'Purchase' | 'Sale' | 'Return' | 'Adjustment' | 'Transfer' | 'Damaged';

export interface StockLevel {
  productId: string;
  productCode: string;
  productName: string;
  categoryName: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  reorderPoint: number;
  safetyStock: number;
  status: StockStatus;
}

export interface WarehouseStock {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
}

export interface InventoryTransaction {
  id: string;
  occurredAtUtc: string;
  type: InventoryTransactionType;
  quantity: number;
  productCode: string;
  productName: string;
  warehouseCode: string;
  reference: string | null;
  notes: string | null;
}

export interface ProductStock {
  productId: string;
  productCode: string;
  productName: string;
  reorderPoint: number;
  safetyStock: number;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  status: StockStatus;
  warehouses: WarehouseStock[];
  recentTransactions: InventoryTransaction[];
}

export interface AdjustStockRequest {
  productId: string;
  warehouseId: string;
  quantity: number;
  reason: 'Adjustment' | 'Damaged' | 'Return';
  notes: string | null;
}

export interface TransferStockRequest {
  productId: string;
  fromWarehouseId: string;
  toWarehouseId: string;
  quantity: number;
  notes: string | null;
}
