export interface SupplierListItem {
  id: string;
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  averageLeadTimeDays: number;
  isActive: boolean;
}

export interface Supplier {
  id: string;
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  paymentTermsDays: number;
  averageLeadTimeDays: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SaveSupplierRequest {
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  paymentTermsDays: number;
  averageLeadTimeDays: number;
  isActive: boolean;
}
