import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';

import { describeApiError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { ActionCard } from '../../components/action-card/action-card';
import { AiApi } from '../../data/ai.api';
import { AiAction, AiActionStatus } from '../../data/ai.models';

/** Design doc §26: one place where approvers review every AI-proposed operation. */
@Component({
  selector: 'app-approvals',
  imports: [DatePipe, RouterLink, MatButtonToggleModule, MatIconModule, MatProgressBarModule, PageHeader, ActionCard],
  templateUrl: './approvals.html',
})
export class Approvals {
  private readonly api = inject(AiApi);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly actions = signal<AiAction[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly filter = signal<AiActionStatus | 'All'>('Pending');

  constructor() {
    this.load();
  }

  protected setFilter(value: AiActionStatus | 'All'): void {
    this.filter.set(value);
    this.load();
  }

  protected onDecided(action: AiAction): void {
    const created = action.output?.purchaseOrders?.map((po) => po.poNumber).join(', ');
    const message =
      action.status === 'Executed' ? `Approved. Created ${created}.`
      : action.status === 'Rejected' ? 'Proposal rejected; nothing was created.'
      : `Approved, but execution failed: ${action.output?.error ?? 'see details'}`;
    this.snackBar.open(message, 'Dismiss', { duration: 6000 });
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    const status = this.filter();
    this.api.actions(status === 'All' ? undefined : status).subscribe({
      next: (actions) => {
        this.actions.set(actions);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(describeApiError(err));
        this.loading.set(false);
      },
    });
  }
}
