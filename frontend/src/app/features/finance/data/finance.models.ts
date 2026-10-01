import { InvoiceStatus } from '../../../shared/models/statuses';

export type PaymentMethod = 'BankTransfer' | 'Card' | 'Check' | 'Cash';

export const PAYMENT_METHODS: { value: PaymentMethod; label: string }[] = [
  { value: 'BankTransfer', label: 'Bank transfer' },
  { value: 'Card', label: 'Card' },
  { value: 'Check', label: 'Check' },
  { value: 'Cash', label: 'Cash' },
];

export interface InvoiceListItem {
  id: string;
  invoiceNumber: string;
  customerName: string;
  salesOrderNumber: string;
  status: InvoiceStatus;
  isOverdue: boolean;
  issueDateUtc: string | null;
  dueDateUtc: string | null;
  totalAmount: number;
  amountPaid: number;
  balance: number;
}

export interface Invoice {
  id: string;
  invoiceNumber: string;
  customerId: string;
  customerName: string;
  salesOrderId: string;
  salesOrderNumber: string;
  status: InvoiceStatus;
  isOverdue: boolean;
  issueDateUtc: string | null;
  dueDateUtc: string | null;
  totalAmount: number;
  amountPaid: number;
  balance: number;
  lines: { id: string; description: string; quantity: number; unitPrice: number; lineTotal: number }[];
  payments: { id: string; amount: number; paidAtUtc: string; method: PaymentMethod; reference: string | null }[];
}

export interface CreateInvoiceRequest {
  salesOrderId: string;
}

export interface RecordPaymentRequest {
  amount: number;
  paidAtUtc: string | null;
  method: PaymentMethod;
  reference: string | null;
}
