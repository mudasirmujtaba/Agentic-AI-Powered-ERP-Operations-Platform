import { Component, computed, input } from '@angular/core';

export type BadgeTone = 'success' | 'warning' | 'neutral' | 'danger' | 'info';

const toneClasses: Record<BadgeTone, { badge: string; dot: string }> = {
  success: { badge: 'bg-emerald-50 text-emerald-800 ring-emerald-200', dot: 'bg-emerald-500' },
  warning: { badge: 'bg-amber-50 text-amber-800 ring-amber-200', dot: 'bg-amber-500' },
  danger: { badge: 'bg-red-50 text-red-800 ring-red-200', dot: 'bg-red-500' },
  info: { badge: 'bg-blue-50 text-blue-800 ring-blue-200', dot: 'bg-blue-500' },
  neutral: { badge: 'bg-slate-50 text-slate-700 ring-slate-200', dot: 'bg-slate-400' },
};

/** Status label with a leading dot; colour supports, never replaces, the text. */
@Component({
  selector: 'app-status-badge',
  template: `<span
    class="inline-flex items-center gap-1.5 whitespace-nowrap rounded px-1.5 py-0.5 text-xs font-medium ring-1 ring-inset"
    [class]="classes().badge"
    ><span class="h-1.5 w-1.5 rounded-full" [class]="classes().dot" aria-hidden="true"></span>{{ label() }}</span
  >`,
})
export class StatusBadge {
  readonly label = input.required<string>();
  readonly tone = input<BadgeTone>('neutral');

  protected readonly classes = computed(() => toneClasses[this.tone()]);
}
