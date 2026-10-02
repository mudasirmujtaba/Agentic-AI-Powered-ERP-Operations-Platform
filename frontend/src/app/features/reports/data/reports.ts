import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { ValueFormat } from '../../../shared/formatting/format';

export type ReportKey = 'sales' | 'inventory' | 'procurement' | 'finance' | 'ai';

export interface ReportKpi {
  label: string;
  value: number | null;
  format: ValueFormat;
  hint: string | null;
}

export interface ReportColumn {
  key: string;
  label: string;
  format: ValueFormat;
}

export interface ReportTable {
  title: string;
  columns: ReportColumn[];
  rows: Record<string, unknown>[];
  emptyText: string | null;
}

export interface Report {
  key: ReportKey;
  title: string;
  from: string;
  to: string;
  generatedAtUtc: string;
  kpis: ReportKpi[];
  trend: { title: string; format: ValueFormat; points: { label: string; value: number }[] } | null;
  tables: ReportTable[];
}

export const REPORT_LABELS: Record<ReportKey, { title: string; description: string }> = {
  sales: { title: 'Sales', description: 'Revenue, orders, average order value and customer revenue' },
  inventory: { title: 'Inventory', description: 'Stock levels and value, low-stock items, turnover and movement' },
  procurement: { title: 'Procurement', description: 'Purchase volume, supplier performance and delivery delays' },
  finance: { title: 'Finance', description: 'Receivables, overdue invoices, aging and payment trends' },
  ai: { title: 'AI operations', description: 'Agent executions, recommendations, approvals, tool usage and errors' },
};

@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/reports`;

  available(): Observable<ReportKey[]> {
    return this.http.get<ReportKey[]>(this.base);
  }

  get(key: ReportKey, from: string, to: string): Observable<Report> {
    return this.http.get<Report>(`${this.base}/${key}`, { params: new HttpParams().set('from', from).set('to', to) });
  }
}

/** CSV for one table, quoted per RFC 4180, with raw numeric values so spreadsheets can compute on them. */
export function toCsv(table: ReportTable): string {
  const escape = (value: unknown) => {
    const text = value === null || value === undefined ? '' : String(value);
    return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
  };
  const header = table.columns.map((c) => escape(c.label)).join(',');
  const rows = table.rows.map((row) => table.columns.map((c) => escape(row[c.key])).join(','));
  return [header, ...rows].join('\r\n');
}
