import { Component, DestroyRef, ElementRef, afterNextRender, computed, inject, input, signal } from '@angular/core';

import { ChartFormat, formatCompact, formatValue } from '../../formatting/format';

export interface BarPoint {
  /** Short axis label, e.g. "Sep". */
  label: string;
  /** Full label for the tooltip and screen readers, e.g. "September 2026". */
  fullLabel?: string;
  value: number;
}

const WIDTH = 640;
const HEIGHT = 240;
const PLOT = { left: 56, right: 12, top: 16, bottom: 28 };
const RADIUS = 4;

interface Bar {
  label: string;
  fullLabel: string;
  value: number;
  x: number;
  width: number;
  bandX: number;
  bandWidth: number;
  y: number;
  path: string;
}

/**
 * Single-series bars (monthly trends on the dashboard and in reports). Thin bars with 4px rounded data-ends anchored to the baseline,
 * recessive grid, a per-bar hover tooltip, and a screen-reader table carrying the same values.
 */
@Component({
  selector: 'app-bar-chart',
  host: { class: 'block' },
  template: `
    <div class="relative">
      <svg
        [attr.viewBox]="'0 0 ' + chartWidth() + ' ' + height"
        class="block h-auto w-full"
        role="img"
        [attr.aria-label]="summary()"
        (mouseleave)="hovered.set(null)"
      >
        @for (tick of ticks(); track tick.value) {
          <line [attr.x1]="plot.left" [attr.x2]="chartWidth() - plot.right" [attr.y1]="tick.y" [attr.y2]="tick.y" class="grid-line" />
          <text [attr.x]="plot.left - 8" [attr.y]="tick.y" text-anchor="end" dominant-baseline="middle" class="axis-text">
            {{ tick.label }}
          </text>
        }

        @for (bar of bars(); track bar.label; let i = $index) {
          <path [attr.d]="bar.path" class="bar" [class.dimmed]="hovered() !== null && hovered() !== i" />
          <text [attr.x]="bar.x + bar.width / 2" [attr.y]="height - 8" text-anchor="middle" class="axis-text">{{ bar.label }}</text>
          <!-- Hit target spans the whole band so thin bars are easy to hover. -->
          <rect
            [attr.x]="bar.bandX"
            [attr.y]="plot.top"
            [attr.width]="bar.bandWidth"
            [attr.height]="baseline - plot.top"
            fill="transparent"
            (mouseenter)="hovered.set(i)"
          />
        }

        <line [attr.x1]="plot.left" [attr.x2]="chartWidth() - plot.right" [attr.y1]="baseline" [attr.y2]="baseline" class="baseline" />
      </svg>

      @if (hoveredBar(); as bar) {
        <div
          class="pointer-events-none absolute px-3 py-2 text-xs tooltip"
          [style.left.%]="((bar.x + bar.width / 2) / chartWidth()) * 100"
          [style.top.%]="(bar.y / height) * 100"
        >
          <div class="font-medium">{{ bar.fullLabel }}</div>
          <div>{{ display(bar.value) }}</div>
        </div>
      }

      <table class="sr-only">
        <caption>{{ caption() }}</caption>
        <tr><th>Period</th><th>Value</th></tr>
        @for (bar of bars(); track bar.label) {
          <tr><td>{{ bar.fullLabel }}</td><td>{{ display(bar.value) }}</td></tr>
        }
      </table>
    </div>
  `,
  styles: `
    .bar { fill: var(--primary-600); transition: opacity 120ms; }
    .bar.dimmed { opacity: 0.45; }
    .grid-line { stroke: var(--gray-100); stroke-width: 1; }
    .baseline { stroke: var(--border-strong); stroke-width: 1; }
    .axis-text { fill: var(--text-muted); font-size: var(--text-xs); font-variant-numeric: tabular-nums; }
    .tooltip {
      transform: translate(-50%, calc(-100% - 8px));
      background: var(--ink-900);
      color: var(--text-on-ink);
      border-radius: var(--radius-md);
      box-shadow: var(--shadow-overlay);
      white-space: nowrap;
    }
  `,
})
export class BarChart {
  readonly points = input.required<BarPoint[]>();
  readonly format = input<ChartFormat>('currency');
  readonly caption = input('Values by period');

  /** Drawn at the container's real width, so text and bars keep their size instead of scaling with the panel. */
  protected readonly chartWidth = signal(WIDTH);
  protected readonly height = HEIGHT;

  constructor() {
    const host = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      const observer = new ResizeObserver(([entry]) => {
        const width = Math.round(entry.contentRect.width);
        if (width > 0) this.chartWidth.set(Math.max(320, width));
      });
      observer.observe(host);
      destroyRef.onDestroy(() => observer.disconnect());
    });
  }
  protected readonly plot = PLOT;
  protected readonly baseline = HEIGHT - PLOT.bottom;
  protected readonly hovered = signal<number | null>(null);

  private readonly maxValue = computed(() => niceCeiling(Math.max(1, ...this.points().map((d) => d.value))));

  protected readonly ticks = computed(() => {
    const max = this.maxValue();
    return [0, 0.25, 0.5, 0.75, 1].map((fraction) => ({
      value: max * fraction,
      y: this.yFor(max * fraction),
      label: formatCompact(max * fraction, this.format()),
    }));
  });

  protected readonly bars = computed<Bar[]>(() => {
    const data = this.points();
    const bandWidth = (this.chartWidth() - PLOT.left - PLOT.right) / Math.max(1, data.length);
    const barWidth = Math.min(36, bandWidth * 0.45);

    return data.map((d, i) => {
      const bandX = PLOT.left + i * bandWidth;
      const x = bandX + (bandWidth - barWidth) / 2;
      const y = this.yFor(d.value);
      return {
        label: d.label,
        fullLabel: d.fullLabel ?? d.label,
        value: d.value,
        x,
        width: barWidth,
        bandX,
        bandWidth,
        y,
        path: roundedTopBar(x, y, barWidth, this.baseline - y),
      };
    });
  });

  protected readonly hoveredBar = computed(() => {
    const index = this.hovered();
    return index === null ? null : (this.bars()[index] ?? null);
  });

  protected readonly summary = computed(() =>
    `${this.caption()}: ` +
    this.bars()
      .map((b) => `${b.fullLabel} ${formatCompact(b.value, this.format())}`)
      .join(', '),
  );

  protected display(value: number): string {
    return formatValue(value, this.format());
  }

  private yFor(value: number): number {
    const plotHeight = this.baseline - PLOT.top;
    return this.baseline - (value / this.maxValue()) * plotHeight;
  }
}

/** Bar path with only the top (data-end) corners rounded; zero-height bars render nothing. */
function roundedTopBar(x: number, y: number, width: number, height: number): string {
  if (height <= 0) return '';
  const r = Math.min(RADIUS, width / 2, height);
  return [
    `M${x},${y + height}`,
    `V${y + r}`,
    `Q${x},${y} ${x + r},${y}`,
    `H${x + width - r}`,
    `Q${x + width},${y} ${x + width},${y + r}`,
    `V${y + height}`,
    'Z',
  ].join(' ');
}

/** Rounds up to 1, 2, 2.5 or 5 × 10^n so gridlines land on readable values. */
function niceCeiling(value: number): number {
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const normalized = value / magnitude;
  const step = [1, 2, 2.5, 5, 10].find((s) => normalized <= s) ?? 10;
  return step * magnitude;
}
