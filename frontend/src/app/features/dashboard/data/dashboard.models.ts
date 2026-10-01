import { SalesOrderStatus } from '../../../shared/models/statuses';
import { StockLevel } from '../../inventory/data/inventory.models';

export interface MonthlyRevenue {
  year: number;
  month: number;
  revenue: number;
}

export interface DashboardSummary {
  revenueLast30Days: number;
  revenueYearToDate: number;
  ordersLast30Days: number;
  openSalesOrders: number;
  lateSalesOrders: number;
  lowStockProducts: number;
  outOfStockProducts: number;
  outstandingReceivables: number;
  overdueInvoices: number;
  overdueAmount: number;
  pendingPurchaseApprovals: number;
  openPurchaseOrders: number;
  revenueByMonth: MonthlyRevenue[];
  topCustomers: { customerId: string; customerName: string; revenue: number }[];
  lowStock: StockLevel[];
  recentOrders: {
    id: string;
    orderNumber: string;
    customerName: string;
    status: SalesOrderStatus;
    orderDateUtc: string;
    totalAmount: number;
  }[];
}
