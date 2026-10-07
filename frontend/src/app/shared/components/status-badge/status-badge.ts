import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type BadgeTone = 'success' | 'warning' | 'neutral' | 'danger' | 'info';

/** Status label with a leading marker; colour supports, never replaces, the text. Semantic tokens only. */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [attr.data-tone]="tone()"><span class="marker" aria-hidden="true"></span>{{ label() }}</span>`,
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      height: 20px;
      padding: 0 var(--space-2);
      border-radius: var(--radius-sm);
      border: 1px solid var(--tone-border);
      background: var(--tone-bg);
      color: var(--tone-fg);
      font-size: var(--text-xs);
      line-height: var(--leading-xs);
      font-weight: var(--weight-medium);
      white-space: nowrap;
    }
    .marker {
      width: 6px;
      height: 6px;
      border-radius: 2px;
      background: currentColor;
    }
    [data-tone='success'] { --tone-fg: var(--success); --tone-bg: var(--success-bg); --tone-border: color-mix(in srgb, var(--success) 22%, transparent); }
    [data-tone='warning'] { --tone-fg: var(--warning); --tone-bg: var(--warning-bg); --tone-border: color-mix(in srgb, var(--warning) 22%, transparent); }
    [data-tone='danger'] { --tone-fg: var(--danger); --tone-bg: var(--danger-bg); --tone-border: color-mix(in srgb, var(--danger) 22%, transparent); }
    [data-tone='info'] { --tone-fg: var(--info); --tone-bg: var(--info-bg); --tone-border: color-mix(in srgb, var(--info) 22%, transparent); }
    [data-tone='neutral'] { --tone-fg: var(--text-secondary); --tone-bg: var(--surface-sunken); --tone-border: var(--border); }
  `,
})
export class StatusBadge {
  readonly label = input.required<string>();
  readonly tone = input<BadgeTone>('neutral');

}
