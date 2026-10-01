import { CurrencyPipe } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';

import { MonthlyRevenue } from '../../data/dashboard.models';

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
 * Single-series monthly revenue bars. Thin bars with 4px rounded data-ends anchored to the baseline,
 * recessive grid, a per-bar hover tooltip, and a screen-reader table carrying the same values.
 */
@Component({
  selector: 'app-revenue-chart',
  imports: [CurrencyPipe],
  template: `
    <div class="relative">
      <svg
        [attr.viewBox]="'0 0 ' + width + ' ' + height"
        class="block h-auto w-full"
        role="img"
        [attr.aria-label]="summary()"
        (mouseleave)="hovered.set(null)"
      >
        @for (tick of ticks(); track tick.value) {
          <line [attr.x1]="plot.left" [attr.x2]="width - plot.right" [attr.y1]="tick.y" [attr.y2]="tick.y" class="grid-line" />
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

        <line [attr.x1]="plot.left" [attr.x2]="width - plot.right" [attr.y1]="baseline" [attr.y2]="baseline" class="baseline" />
      </svg>

      @if (hoveredBar(); as bar) {
        <div
          class="pointer-events-none absolute rounded-md px-3 py-2 text-xs shadow-md tooltip"
          [style.left.%]="((bar.x + bar.width / 2) / width) * 100"
          [style.top.%]="(bar.y / height) * 100"
        >
          <div class="font-medium">{{ bar.fullLabel }}</div>
          <div>{{ bar.value | currency: 'USD' : 'symbol' : '1.0-0' }}</div>
        </div>
      }

      <table class="sr-only">
        <caption>Invoiced revenue by month</caption>
        <tr><th>Month</th><th>Revenue</th></tr>
        @for (bar of bars(); track bar.label) {
          <tr><td>{{ bar.fullLabel }}</td><td>{{ bar.value | currency: 'USD' : 'symbol' : '1.0-0' }}</td></tr>
        }
      </table>
    </div>
  `,
  styles: `
    .bar { fill: var(--mat-sys-primary); transition: opacity 120ms; }
    .bar.dimmed { opacity: 0.45; }
    .grid-line { stroke: var(--mat-sys-outline-variant); stroke-width: 1; }
    .baseline { stroke: var(--mat-sys-outline); stroke-width: 1; }
    .axis-text { fill: var(--mat-sys-on-surface-variant); font-size: 12px; }
    .tooltip {
      transform: translate(-50%, calc(-100% - 8px));
      background: var(--mat-sys-inverse-surface);
      color: var(--mat-sys-inverse-on-surface);
      white-space: nowrap;
    }
  `,
})
export class RevenueChart {
  readonly data = input.required<MonthlyRevenue[]>();

  protected readonly width = WIDTH;
  protected readonly height = HEIGHT;
  protected readonly plot = PLOT;
  protected readonly baseline = HEIGHT - PLOT.bottom;
  protected readonly hovered = signal<number | null>(null);

  private readonly maxValue = computed(() => niceCeiling(Math.max(1, ...this.data().map((d) => d.revenue))));

  protected readonly ticks = computed(() => {
    const max = this.maxValue();
    return [0, 0.25, 0.5, 0.75, 1].map((fraction) => ({
      value: max * fraction,
      y: this.yFor(max * fraction),
      label: compactCurrency(max * fraction),
    }));
  });

  protected readonly bars = computed<Bar[]>(() => {
    const data = this.data();
    const bandWidth = (WIDTH - PLOT.left - PLOT.right) / Math.max(1, data.length);
    const barWidth = Math.min(36, bandWidth * 0.45);

    return data.map((d, i) => {
      const date = new Date(Date.UTC(d.year, d.month - 1, 1));
      const bandX = PLOT.left + i * bandWidth;
      const x = bandX + (bandWidth - barWidth) / 2;
      const y = this.yFor(d.revenue);
      return {
        label: date.toLocaleString('en-US', { month: 'short', timeZone: 'UTC' }),
        fullLabel: date.toLocaleString('en-US', { month: 'long', year: 'numeric', timeZone: 'UTC' }),
        value: d.revenue,
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
    'Invoiced revenue by month: ' +
    this.bars()
      .map((b) => `${b.fullLabel} ${compactCurrency(b.value)}`)
      .join(', '),
  );

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

function compactCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', notation: 'compact', maximumFractionDigits: 1 }).format(value);
}
