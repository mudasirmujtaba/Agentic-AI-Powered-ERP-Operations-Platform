import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';

import { describeApiError } from '../../../../core/http/api-error';
import { BarChart, BarPoint } from '../../../../shared/components/bar-chart/bar-chart';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { formatReportValue, isNumericFormat, ValueFormat } from '../../../../shared/formatting/format';
import { REPORT_LABELS, Report, ReportKey, ReportTable, ReportsApi, toCsv } from '../../data/reports';
import { Icon } from '../../../../shared/components/icon/icon';

type Preset = '30d' | '90d' | 'ytd' | '12m' | 'custom';

const PRESETS: { value: Preset; label: string }[] = [
  { value: '30d', label: 'Last 30 days' },
  { value: '90d', label: 'Last 90 days' },
  { value: 'ytd', label: 'Year to date' },
  { value: '12m', label: 'Last 12 months' },
  { value: 'custom', label: 'Custom range' },
];

function isoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function rangeFor(preset: Exclude<Preset, 'custom'>): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  if (preset === '30d') from.setUTCDate(to.getUTCDate() - 29);
  if (preset === '90d') from.setUTCDate(to.getUTCDate() - 89);
  if (preset === 'ytd') from.setUTCMonth(0, 1);
  if (preset === '12m') from.setUTCMonth(to.getUTCMonth() - 11, 1);
  return { from: isoDate(from), to: isoDate(to) };
}

@Component({
  selector: 'app-reports',
  imports: [Icon, 
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    BarChart,
    PageHeader,
  ],
  templateUrl: './reports.html',
  styleUrl: './reports.css',
})
export class Reports implements OnInit {
  private readonly api = inject(ReportsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly labels = REPORT_LABELS;
  protected readonly presets = PRESETS;
  protected readonly format = formatReportValue;
  protected readonly numeric = isNumericFormat;

  protected readonly available = signal<ReportKey[]>([]);
  protected readonly active = signal<ReportKey | null>(null);
  protected readonly preset = signal<Preset>('90d');
  protected readonly from = signal(rangeFor('90d').from);
  protected readonly to = signal(rangeFor('90d').to);
  protected readonly report = signal<Report | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly trendPoints = computed<BarPoint[]>(() =>
    (this.report()?.trend?.points ?? []).map((p) => ({ label: p.label.slice(0, 3), fullLabel: p.label, value: p.value })),
  );
  protected readonly trendFormat = computed(() => (this.report()?.trend?.format === 'Currency' ? 'currency' : 'number'));

  ngOnInit(): void {
    this.api.available().subscribe({
      next: (keys) => {
        this.available.set(keys);
        const requested = this.route.snapshot.queryParamMap.get('report') as ReportKey | null;
        this.select(requested && keys.includes(requested) ? requested : keys[0] ?? null);
      },
      error: (err) => this.error.set(describeApiError(err)),
    });
  }

  protected select(key: ReportKey | null): void {
    this.active.set(key);
    if (!key) return;
    this.router.navigate([], { queryParams: { report: key }, queryParamsHandling: 'merge', replaceUrl: true });
    this.load();
  }

  protected setPreset(preset: Preset): void {
    this.preset.set(preset);
    if (preset !== 'custom') {
      const range = rangeFor(preset);
      this.from.set(range.from);
      this.to.set(range.to);
      this.load();
    }
  }

  protected applyCustom(): void {
    if (this.from() && this.to()) this.load();
  }

  protected exportCsv(table: ReportTable): void {
    const report = this.report();
    if (!report) return;
    const blob = new Blob([toCsv(table)], { type: 'text/csv;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `opspilot-${report.key}-${table.title.toLowerCase().replace(/[^a-z0-9]+/g, '-')}-${report.from}-to-${report.to}.csv`;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  protected cell(value: unknown, format: ValueFormat): string {
    return formatReportValue(value, format, format === 'Currency');
  }

  private load(): void {
    const key = this.active();
    if (!key) return;
    this.loading.set(true);
    this.error.set(null);
    this.api.get(key, this.from(), this.to()).subscribe({
      next: (report) => {
        this.report.set(report);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(describeApiError(err));
        this.loading.set(false);
      },
    });
  }
}
