export interface Warehouse {
  id: string;
  code: string;
  name: string;
  location: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SaveWarehouseRequest {
  code: string;
  name: string;
  location: string | null;
  isActive: boolean;
}
