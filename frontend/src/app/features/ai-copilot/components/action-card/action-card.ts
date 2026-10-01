import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError } from '../../../../core/http/api-error';
import { BadgeTone, StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { AiApi } from '../../data/ai.api';
import { AiAction, AiActionStatus } from '../../data/ai.models';

const STATUS: Record<AiActionStatus, { label: string; tone: BadgeTone }> = {
  Pending: { label: 'Awaiting approval', tone: 'warning' },
  Approved: { label: 'Approved', tone: 'success' },
  Executed: { label: 'Approved · POs created', tone: 'success' },
  Rejected: { label: 'Rejected', tone: 'neutral' },
  Failed: { label: 'Approved · execution failed', tone: 'danger' },
};

/**
 * Human-in-the-loop review of an AI proposal (design doc §26: Approve, Reject, Modify).
 * Approving sends the (possibly edited) quantities to the ERP, which creates draft POs through its normal rules.
 */
@Component({
  selector: 'app-action-card',
  imports: [CurrencyPipe, DatePipe, FormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, StatusBadge],
  templateUrl: './action-card.html',
})
export class ActionCard {
  readonly action = input.required<AiAction>();
  readonly decided = output<AiAction>();

  private readonly api = inject(AiApi);
  private readonly auth = inject(AuthService);

  protected readonly statusStyle = computed(() => STATUS[this.action().status]);
  protected readonly canDecide = computed(() => this.action().status === 'Pending' && this.auth.hasAnyRole(WriteAccess.purchaseOrders));
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly quantities = signal<Partial<Record<string, number>>>({});
  protected comments = '';

  protected readonly total = computed(() => {
    const quantities = this.quantities();
    return this.action().input.orders
      .flatMap((o) => o.lines)
      .reduce((sum, line) => sum + (quantities[line.productId] ?? line.quantity) * line.unitCost, 0);
  });

  constructor() {
    effect(() => {
      const initial: Record<string, number> = {};
      for (const line of this.action().input.orders.flatMap((o) => o.lines)) {
        initial[line.productId] = line.quantity;
      }
      this.quantities.set(initial);
    });
  }

  protected setQuantity(productId: string, value: number | string): void {
    const quantity = Math.max(0, Math.floor(Number(value) || 0));
    this.quantities.update((q) => ({ ...q, [productId]: quantity }));
  }

  protected approve(): void {
    const changed = this.action()
      .input.orders.flatMap((o) => o.lines)
      .filter((line) => (this.quantities()[line.productId] ?? line.quantity) !== line.quantity)
      .map((line) => ({ productId: line.productId, quantity: this.quantities()[line.productId] ?? line.quantity }));
    this.decide(this.api.approve(this.action().id, { comments: this.comments.trim() || null, lines: changed }));
  }

  protected reject(): void {
    this.decide(this.api.reject(this.action().id, { comments: this.comments.trim() || null }));
  }

  private decide(request$: ReturnType<AiApi['approve']>): void {
    this.busy.set(true);
    this.error.set(null);
    request$.subscribe({
      next: (action) => {
        this.busy.set(false);
        this.decided.emit(action);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(describeApiError(err));
      },
    });
  }
}
