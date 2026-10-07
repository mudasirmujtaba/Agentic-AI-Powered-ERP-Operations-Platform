import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { filter, interval, switchMap, take, timeout } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { BadgeTone, StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { MarkdownLitePipe } from '../../../../shared/pipes/markdown-lite.pipe';
import { AutomationApi } from '../../data/automation.api';
import { InsightReport, JobStatus, RiskLevel } from '../../data/automation.models';
import { Icon } from '../../../../shared/components/icon/icon';

const RISK_TONES: Record<RiskLevel, BadgeTone> = { critical: 'danger', high: 'danger', medium: 'warning', ok: 'success' };
const STATE_TONES: Record<string, BadgeTone> = { Succeeded: 'success', Failed: 'danger', Processing: 'warning', Enqueued: 'warning' };

@Component({
  selector: 'app-automation',
  imports: [Icon, DatePipe, MatButtonModule, MatProgressBarModule, MarkdownLitePipe, PageHeader, StatusBadge],
  templateUrl: './automation.html',
  styles: `
    :host ::ng-deep .answer p { margin: 0 0 0.5rem; }
    :host ::ng-deep .answer ul, :host ::ng-deep .answer ol { margin: 0 0 0.5rem; padding-left: 1.25rem; }
    :host ::ng-deep .answer ul { list-style: disc; }
    :host ::ng-deep .answer ol { list-style: decimal; }
  `,
})
export class Automation implements OnInit {
  private readonly api = inject(AutomationApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly auth = inject(AuthService);

  protected readonly canManageJobs = computed(() => this.auth.hasAnyRole(WriteAccess.jobs));
  protected readonly report = signal<InsightReport | null>(null);
  protected readonly reportLoading = signal(true);
  protected readonly jobs = signal<JobStatus[]>([]);
  protected readonly error = signal<string | null>(null);
  /** Ids of jobs triggered from this page and not yet finished. */
  protected readonly running = signal<ReadonlySet<string>>(new Set());

  ngOnInit(): void {
    this.loadReport();
    if (this.canManageJobs()) this.loadJobs();
  }

  protected riskTone(risk: RiskLevel): BadgeTone {
    return RISK_TONES[risk];
  }

  protected stateTone(state: string | null): BadgeTone {
    return (state && STATE_TONES[state]) || 'neutral';
  }

  protected run(job: JobStatus): void {
    const before = job.lastRunUtc;
    this.setRunning(job.id, true);
    this.api.runJob(job.id).subscribe({
      next: () => {
        this.snackBar.open(`${job.name} started.`, 'Dismiss', { duration: 3000 });
        // Poll until the run is recorded as finished; jobs that call the AI can take a while.
        interval(3000)
          .pipe(
            switchMap(() => this.api.jobs()),
            filter((jobs) => {
              this.jobs.set(jobs);
              const current = jobs.find((j) => j.id === job.id);
              return !!current && current.lastRunUtc !== before && (current.lastState === 'Succeeded' || current.lastState === 'Failed');
            }),
            take(1),
            timeout(240_000),
            takeUntilDestroyed(this.destroyRef),
          )
          .subscribe({
            next: (jobs) => {
              const done = jobs.find((j) => j.id === job.id);
              this.setRunning(job.id, false);
              this.snackBar.open(`${job.name}: ${done?.lastResult ?? done?.lastState}`, 'Dismiss', { duration: 5000 });
              if (job.id === 'inventory-risk-scan') this.loadReport();
            },
            error: () => {
              this.setRunning(job.id, false);
              this.error.set(`${job.name} is taking longer than expected; check back shortly.`);
            },
          });
      },
      error: (err) => {
        this.setRunning(job.id, false);
        this.error.set(describeApiError(err));
      },
    });
  }

  private setRunning(id: string, on: boolean): void {
    const next = new Set(this.running());
    if (on) next.add(id);
    else next.delete(id);
    this.running.set(next);
  }

  private loadReport(): void {
    this.reportLoading.set(true);
    this.api.latestInventoryRisk().subscribe({
      next: (report) => {
        this.report.set(report);
        this.reportLoading.set(false);
      },
      error: (err) => {
        this.error.set(describeApiError(err));
        this.reportLoading.set(false);
      },
    });
  }

  private loadJobs(): void {
    this.api.jobs().subscribe({
      next: (jobs) => this.jobs.set(jobs),
      error: (err) => this.error.set(describeApiError(err)),
    });
  }
}
