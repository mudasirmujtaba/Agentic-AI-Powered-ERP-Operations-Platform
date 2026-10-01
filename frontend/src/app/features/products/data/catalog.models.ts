export interface Category {
  id: string;
  name: string;
  description: string | null;
  productCount: number;
}

export interface SaveCategoryRequest {
  name: string;
  description: string | null;
}

export interface ProductListItem {
  id: string;
  code: string;
  name: string;
  categoryName: string;
  unitPrice: number;
  cost: number;
  reorderPoint: number;
  safetyStock: number;
  primarySupplierName: string | null;
  isActive: boolean;
}

export interface Product {
  id: string;
  code: string;
  name: string;
  description: string | null;
  categoryId: string;
  categoryName: string;
  unitPrice: number;
  cost: number;
  reorderPoint: number;
  safetyStock: number;
  primarySupplierId: string | null;
  primarySupplierName: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SaveProductRequest {
  code: string;
  name: string;
  description: string | null;
  categoryId: string;
  unitPrice: number;
  cost: number;
  reorderPoint: number;
  safetyStock: number;
  primarySupplierId: string | null;
  isActive: boolean;
}
