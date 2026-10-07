import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';
import { describeApiError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { SALES_ORDER_STATUSES, STOCK_STATUSES } from '../../../../shared/models/statuses';
import { BarChart, BarPoint } from '../../../../shared/components/bar-chart/bar-chart';
import { DashboardApi } from '../../data/dashboard.api';
import { DashboardSummary } from '../../data/dashboard.models';
import { Icon, IconName } from '../../../../shared/components/icon/icon';

interface KpiTile {
  label: string;
  value: string;
  detail: string;
  /** Present only when something needs attention; always shown with an icon and words, never color alone. */
  alert?: { icon: IconName; text: string } | undefined;
  link: string;
  queryParams?: Record<string, string> | undefined;
}

@Component({
  selector: 'app-dashboard',
  imports: [Icon, CurrencyPipe, DatePipe, RouterLink, MatButtonModule, MatProgressBarModule, PageHeader, StatusBadge, BarChart],
  templateUrl: './dashboard.html',
})
export class Dashboard {
  private readonly api = inject(DashboardApi);
  private readonly auth = inject(AuthService);

  protected readonly orderStatuses = SALES_ORDER_STATUSES;
  protected readonly stockStatuses = STOCK_STATUSES;
  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly loading = signal(true);

  protected readonly revenuePoints = computed<BarPoint[]>(() =>
    (this.summary()?.revenueByMonth ?? []).map((m) => {
      const date = new Date(Date.UTC(m.year, m.month - 1, 1));
      return {
        label: date.toLocaleString('en-US', { month: 'short', timeZone: 'UTC' }),
        fullLabel: date.toLocaleString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' }),
        value: m.revenue,
      };
    }),
  );
  protected readonly error = signal<string | null>(null);
  protected readonly greeting = computed(() => {
    const name = this.auth.currentUser()?.firstName;
    return name ? `Welcome back, ${name}` : 'Overview';
  });

  protected readonly tiles = computed<KpiTile[]>(() => {
    const s = this.summary();
    if (!s) return [];
    const usd = (v: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(v);

    const tiles: KpiTile[] = [
      {
        label: 'Revenue, last 30 days',
        value: usd(s.revenueLast30Days),
        detail: `${usd(s.revenueYearToDate)} year to date`,
        link: '/finance',
      },
      {
        label: 'Open sales orders',
        value: `${s.openSalesOrders}`,
        detail: `${s.ordersLast30Days} orders in the last 30 days`,
        alert: s.lateSalesOrders > 0 ? { icon: 'clock', text: `${s.lateSalesOrders} past required date` } : undefined,
        link: '/sales',
        queryParams: s.lateSalesOrders > 0 ? { lateOnly: 'true' } : undefined,
      },
      {
        label: 'Products to reorder',
        value: `${s.lowStockProducts}`,
        detail: 'At or below reorder point',
        alert: s.outOfStockProducts > 0 ? { icon: 'circle-alert', text: `${s.outOfStockProducts} out of stock` } : undefined,
        link: '/inventory',
        queryParams: { lowStockOnly: 'true' },
      },
      {
        label: 'Outstanding receivables',
        value: usd(s.outstandingReceivables),
        detail: 'Issued and partially paid invoices',
        alert: s.overdueInvoices > 0 ? { icon: 'triangle-alert', text: `${s.overdueInvoices} overdue · ${usd(s.overdueAmount)}` } : undefined,
        link: '/finance',
        queryParams: s.overdueInvoices > 0 ? { overdueOnly: 'true' } : undefined,
      },
      {
        label: 'Purchase approvals',
        value: `${s.pendingPurchaseApprovals}`,
        detail: `${s.openPurchaseOrders} purchase orders in progress`,
        alert: s.pendingPurchaseApprovals > 0 ? { icon: 'hourglass', text: 'Waiting for a manager' } : undefined,
        link: '/purchasing',
        queryParams: { status: 'PendingApproval' },
      },
    ];
    return tiles;
  });

  constructor() {
    this.api.summary().subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(describeApiError(err));
        this.loading.set(false);
      },
    });
  }
}
