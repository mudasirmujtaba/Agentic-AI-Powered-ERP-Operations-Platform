import { TicketCategory, TicketPriority, TicketStatus } from '../../../shared/models/statuses';

export interface TicketListItem {
  id: string;
  ticketNumber: string;
  subject: string;
  customerName: string;
  productCode: string | null;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  assignedToName: string | null;
  commentCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface TicketComment {
  id: string;
  authorName: string;
  body: string;
  isInternal: boolean;
  createdAtUtc: string;
}

export interface Ticket {
  id: string;
  ticketNumber: string;
  subject: string;
  description: string;
  customerId: string;
  customerName: string;
  salesOrderId: string | null;
  orderNumber: string | null;
  productId: string | null;
  productCode: string | null;
  productName: string | null;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  allowedNextStatuses: TicketStatus[];
  assignedToUserId: string | null;
  assignedToName: string | null;
  resolution: string | null;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  aiSummary: string | null;
  aiSummaryAtUtc: string | null;
  comments: TicketComment[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SaveTicketRequest {
  subject: string;
  description: string;
  customerId: string;
  salesOrderId: string | null;
  productId: string | null;
  category: TicketCategory;
  priority: TicketPriority;
  assignedToUserId: string | null;
}

export interface TicketAssignee {
  id: string;
  name: string;
  email: string;
}

export interface TicketStats {
  open: number;
  urgent: number;
  unassigned: number;
  assignedToMe: number;
  resolvedLast30Days: number;
}

export interface TicketInsights {
  content: string;
  ticketsAnalysed: number;
  generatedAtUtc: string;
}
