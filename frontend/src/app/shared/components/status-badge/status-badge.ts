import { Component, computed, input } from '@angular/core';

export type BadgeTone = 'success' | 'warning' | 'neutral' | 'danger';

const toneClasses: Record<BadgeTone, string> = {
  success: 'bg-green-100 text-green-800',
  warning: 'bg-amber-100 text-amber-800',
  danger: 'bg-red-100 text-red-800',
  neutral: 'bg-gray-200 text-gray-700',
};

@Component({
  selector: 'app-status-badge',
  template: `<span class="inline-flex whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium" [class]="classes()">{{ label() }}</span>`,
})
export class StatusBadge {
  readonly label = input.required<string>();
  readonly tone = input<BadgeTone>('neutral');

  protected readonly classes = computed(() => toneClasses[this.tone()]);
}
