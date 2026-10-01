export type AiActionStatus = 'Pending' | 'Approved' | 'Rejected' | 'Executed' | 'Failed';

export interface TraceStep {
  node: string;
  ms: number;
  status: 'ok' | 'error';
  kind?: 'tool';
  detail?: string;
}

export interface Citation {
  index: number;
  document: string;
  section: string;
  source: string;
  snippet: string;
}

export interface DataTable {
  columns: string[];
  rows: unknown[][];
  truncated: boolean;
}

export interface MessageMetadata {
  intent?: string;
  data?: DataTable | null;
  sql?: string | null;
  citations?: Citation[];
  trace?: TraceStep[];
  usage?: { input_tokens?: number; output_tokens?: number };
  model?: string;
  durationMs?: number;
  awaitingApproval?: boolean;
  actionId?: string;
  error?: string;
}

export interface ProposedLine {
  productId: string;
  productCode: string;
  productName: string | null;
  quantity: number;
  unitCost: number;
  reason: string | null;
}

export interface ProposedOrder {
  supplierId: string;
  supplierName: string;
  warehouseId: string;
  warehouseCode: string | null;
  lines: ProposedLine[];
}

export interface AiAction {
  id: string;
  conversationId: string;
  agent: string;
  actionType: string;
  summary: string;
  status: AiActionStatus;
  input: { orders: ProposedOrder[]; rationale?: string | null };
  output: { purchaseOrders?: { id: string; poNumber: string; supplierName: string; totalAmount: number; requiresApproval: boolean }[]; error?: string } | null;
  requestedByEmail: string | null;
  createdAtUtc: string;
  decision: { status: string; decidedByEmail: string | null; decidedAtUtc: string; comments: string | null } | null;
}

export interface AiMessage {
  id: string;
  role: 'User' | 'Assistant';
  content: string;
  metadata: MessageMetadata | null;
  createdAtUtc: string;
  action: AiAction | null;
}

export interface AiConversationSummary {
  id: string;
  title: string;
  lastMessageAtUtc: string;
}

export interface AiConversation {
  id: string;
  title: string;
  messages: AiMessage[];
}

export interface AiChatResponse {
  conversationId: string;
  title: string;
  messages: AiMessage[];
}

export interface DecideActionRequest {
  comments: string | null;
  lines?: { productId: string; quantity: number }[];
}
