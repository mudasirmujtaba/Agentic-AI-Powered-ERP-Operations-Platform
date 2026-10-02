import { Component, input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  template: `
    <header class="mb-5 flex flex-wrap items-end justify-between gap-4">
      <div class="min-w-0">
        @if (eyebrow()) {
          <div class="mb-1 text-xs font-medium uppercase tracking-wide text-muted">{{ eyebrow() }}</div>
        }
        <div class="flex flex-wrap items-center gap-2.5">
          <h1 class="m-0 text-[22px] font-semibold leading-tight">{{ title() }}</h1>
          <ng-content select="[badges]" />
        </div>
        @if (subtitle()) {
          <p class="m-0 mt-1 text-sm text-muted">{{ subtitle() }}</p>
        }
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <ng-content />
      </div>
    </header>
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly eyebrow = input<string>();
}
