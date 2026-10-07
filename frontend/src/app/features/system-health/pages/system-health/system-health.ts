import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { switchMap, timer } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import { describeApiError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { Icon } from '../../../../shared/components/icon/icon';

interface Latency {
  name: string;
  count: number;
  failures: number;
  p50Ms: number;
  p95Ms: number;
  maxMs: number;
}

interface Summary {
  sinceUtc: string;
  http: Latency;
  slowestRoutes: Latency[];
  database: Latency;
  aiOperations: Latency[];
  agentIntents: Latency[];
  inputTokens: number;
  outputTokens: number;
  tools: { tool: string; calls: number; failures: number }[];
  jobs: Latency[];
}

const REFRESH_MS = 30_000;

const LABELS: Record<string, string> = {
  erp_query: 'ERP query',
  order_investigation: 'Order investigation',
  inventory_risk: 'Inventory intelligence',
  purchase_recommendation: 'Procurement',
  policy: 'Policy (RAG)',
  general: 'General',
  'inventory-risk-scan': 'Inventory risk scan',
  'credit-hold-review': 'Credit hold review',
  'overdue-invoice-reminders': 'Overdue invoice reminders',
};

/** Live operational metrics (design doc §42), from the API's in-process aggregation; refreshes every 30 s. */
@Component({
  selector: 'app-system-health',
  imports: [Icon, DatePipe, DecimalPipe, PercentPipe, MatButtonModule, PageHeader, StatusBadge],
  templateUrl: './system-health.html',
})
export class SystemHealth implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly summary = signal<Summary | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly refreshedAt = signal<Date | null>(null);

  ngOnInit(): void {
    timer(0, REFRESH_MS)
      .pipe(
        switchMap(() => this.http.get<Summary>(`${environment.apiBaseUrl}/observability/summary`)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (summary) => {
          this.summary.set(summary);
          this.refreshedAt.set(new Date());
          this.error.set(null);
        },
        error: (err) => this.error.set(describeApiError(err)),
      });
  }

  protected refresh(): void {
    this.http.get<Summary>(`${environment.apiBaseUrl}/observability/summary`).subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.refreshedAt.set(new Date());
      },
      error: (err) => this.error.set(describeApiError(err)),
    });
  }

  protected errorRate(l: Latency): number {
    return l.count ? l.failures / l.count : 0;
  }

  protected toolFailures(s: Summary): number {
    return s.tools.reduce((sum, t) => sum + t.failures, 0);
  }

  protected aiCalls(s: Summary): number {
    return s.aiOperations.reduce((sum, o) => sum + o.count, 0);
  }

  protected aiFailures(s: Summary): number {
    return s.aiOperations.reduce((sum, o) => sum + o.failures, 0);
  }

  protected humanize(value: string): string {
    return LABELS[value] ?? value.replace(/_/g, ' ').replace(/\//g, ' / ').replace(/^\w/, (c) => c.toUpperCase());
  }
}
