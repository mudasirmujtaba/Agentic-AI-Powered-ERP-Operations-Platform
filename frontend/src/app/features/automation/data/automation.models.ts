export interface JobStatus {
  id: string;
  name: string;
  description: string;
  schedule: string;
  nextRunUtc: string | null;
  lastRunUtc: string | null;
  lastState: string | null;
  lastResult: string | null;
}

export type RiskLevel = 'critical' | 'high' | 'medium' | 'ok';

export interface InventoryRiskItem {
  product: string;
  name: string;
  risk: RiskLevel;
  available: number;
  safetyStock: number;
  reorderPoint: number;
  daysOfCover: number | null;
  stockoutDate: string | null;
  incoming: number;
  nextDelivery: string | null;
  leadTimeDays: number;
  recommendedQuantity: number;
}

export interface InsightReport {
  id: string;
  kind: string;
  generatedAtUtc: string;
  summary: string;
  aiGenerated: boolean;
  itemCount: number;
  items: InventoryRiskItem[];
}

export type NotificationSeverity = 'Info' | 'Warning' | 'Critical';

export interface AppNotification {
  id: string;
  title: string;
  body: string;
  link: string | null;
  severity: NotificationSeverity;
  source: string;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface NotificationFeed {
  items: AppNotification[];
  unreadCount: number;
}
