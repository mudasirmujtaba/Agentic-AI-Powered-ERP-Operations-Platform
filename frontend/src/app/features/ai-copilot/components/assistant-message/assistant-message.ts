import { DecimalPipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

import { MarkdownLitePipe } from '../../../../shared/pipes/markdown-lite.pipe';
import { AiAction, AiMessage } from '../../data/ai.models';
import { ActionCard } from '../action-card/action-card';

const INTENT_LABELS: Record<string, string> = {
  erp_query: 'ERP query',
  order_investigation: 'Order investigation',
  inventory_risk: 'Inventory intelligence',
  purchase_recommendation: 'Procurement',
  policy: 'Policy (RAG)',
  general: 'General',
  approval_outcome: 'Approval outcome',
  error: 'Unavailable',
};

@Component({
  selector: 'app-assistant-message',
  imports: [DecimalPipe, MatIconModule, MarkdownLitePipe, ActionCard],
  templateUrl: './assistant-message.html',
  styles: `
    :host ::ng-deep .answer p { margin: 0 0 0.5rem; }
    :host ::ng-deep .answer ul, :host ::ng-deep .answer ol { margin: 0 0 0.5rem; padding-left: 1.25rem; }
    :host ::ng-deep .answer ul { list-style: disc; }
    :host ::ng-deep .answer ol { list-style: decimal; }
    :host ::ng-deep .answer code { font-size: 0.85em; background: var(--mat-sys-surface-container); padding: 0 0.25rem; border-radius: 4px; }
    :host ::ng-deep .answer .citation { color: var(--mat-sys-primary); font-weight: 500; }
    details > summary { cursor: pointer; }
  `,
})
export class AssistantMessage {
  readonly message = input.required<AiMessage>();
  readonly actionDecided = output<AiAction>();

  protected readonly meta = computed(() => this.message().metadata ?? {});
  protected readonly intentLabel = computed(() => INTENT_LABELS[this.meta().intent ?? ''] ?? this.meta().intent);
  protected readonly totalTokens = computed(() => (this.meta().usage?.input_tokens ?? 0) + (this.meta().usage?.output_tokens ?? 0));

  protected format(value: unknown): string {
    if (value === null || value === undefined) return '—';
    if (typeof value === 'number') return Number.isInteger(value) ? value.toLocaleString() : value.toLocaleString(undefined, { maximumFractionDigits: 2 });
    if (typeof value === 'boolean') return value ? 'Yes' : 'No';
    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) return value.substring(0, 10);
    return String(value);
  }

  protected header(column: string): string {
    return column.replace(/_/g, ' ');
  }
}
