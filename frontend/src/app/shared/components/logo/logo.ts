import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** The logomark path (also in src/assets/brand/logomark.svg): a compass needle knocked out of a rounded tile. */
const LOGOMARK_PATH =
  'M5.5 1h13A4.5 4.5 0 0 1 23 5.5v13a4.5 4.5 0 0 1-4.5 4.5h-13A4.5 4.5 0 0 1 1 18.5v-13A4.5 4.5 0 0 1 5.5 1Z' +
  'M17.233 6.767 14.333 13.485 10.515 9.667Z' +
  'M9.667 10.515 13.485 14.333 6.767 17.233Z';

/**
 * OpsPilot logo. `variant` names the background it sits on: 'light' (primary mark, dark wordmark) or 'dark'
 * (white mark and wordmark, for ink surfaces such as the sidebar).
 */
@Component({
  selector: 'app-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'app-logo',
    '[class.on-dark]': "variant() === 'dark'",
    role: 'img',
    'aria-label': 'OpsPilot',
  },
  template: `
    <svg class="mark" [attr.width]="size()" [attr.height]="size()" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <path fill="currentColor" fill-rule="evenodd" [attr.d]="path" />
    </svg>
    @if (showWordmark()) {
      <span class="wordmark" [style.font-size.px]="size() * 0.72" aria-hidden="true">OpsPilot</span>
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
      color: var(--text-primary);
    }
    .mark {
      display: block;
      flex-shrink: 0;
      color: var(--primary-600);
    }
    .wordmark {
      font-family: var(--font-sans);
      font-weight: var(--weight-semibold);
      letter-spacing: var(--tracking-tight);
      line-height: 1;
      white-space: nowrap;
    }
    :host(.on-dark) {
      color: var(--text-on-ink);
    }
    :host(.on-dark) .mark {
      color: var(--text-on-ink);
    }
  `,
})
export class Logo {
  readonly variant = input<'light' | 'dark'>('light');
  readonly showWordmark = input(true);
  /** Mark height in px; the wordmark scales with it. */
  readonly size = input(24);

  protected readonly path = LOGOMARK_PATH;
}
