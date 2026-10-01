export type CustomerStatus = 'Active' | 'OnHold' | 'Inactive';
export type AddressType = 'Billing' | 'Shipping';

export const CUSTOMER_STATUSES: { value: CustomerStatus; label: string }[] = [
  { value: 'Active', label: 'Active' },
  { value: 'OnHold', label: 'On hold' },
  { value: 'Inactive', label: 'Inactive' },
];

export interface CustomerAddress {
  id?: string | null;
  type: AddressType;
  line1: string;
  line2?: string | null;
  city: string;
  state?: string | null;
  postalCode?: string | null;
  country: string;
  isDefault: boolean;
}

export interface CustomerListItem {
  id: string;
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  creditLimit: number;
  status: CustomerStatus;
}

export interface Customer {
  id: string;
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  creditLimit: number;
  paymentTermsDays: number;
  status: CustomerStatus;
  addresses: CustomerAddress[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SaveCustomerRequest {
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  creditLimit: number;
  paymentTermsDays: number;
  status: CustomerStatus;
  addresses: CustomerAddress[];
}
