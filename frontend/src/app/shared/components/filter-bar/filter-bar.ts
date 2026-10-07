import { ChangeDetectionStrategy, Component, ElementRef, effect, input, output, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { Icon } from '../icon/icon';

/**
 * Compact filter row for list pages: one horizontal row above the table, 8px gaps, wraps when narrow. Put
 * <app-filter-search>, <app-filter-select> and filter switches inside. When `active`, a ghost "Clear filters" button
 * appears at the end.
 */
@Component({
  selector: 'app-filter-bar',
  imports: [MatButtonModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'filter-bar', role: 'search' },
  template: `
    <ng-content />
    @if (active()) {
      <button mat-button type="button" class="btn-sm" (click)="clear.emit()">
        <app-icon name="x" />
        Clear filters
      </button>
    }
  `,
})
export class FilterBar {
  readonly active = input(false);
  readonly clear = output<void>();
}

/** 32px search input with a leading search icon. Emits every keystroke; callers debounce. */
@Component({
  selector: 'app-filter-search',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'filter-search' },
  template: `
    <app-icon name="search" class="filter-search-icon" />
    <input
      #box
      type="search"
      [attr.placeholder]="placeholder()"
      [attr.aria-label]="label() || placeholder()"
      [value]="text()"
      (input)="onInput(box.value)"
    />
  `,
})
export class FilterSearch {
  readonly placeholder = input('Search…');
  readonly label = input<string>();
  /** The applied search term. External changes (e.g. clearing filters) are reflected unless the user is typing. */
  readonly value = input<string | undefined>('');
  readonly valueChange = output<string>();

  private readonly box = viewChild.required<ElementRef<HTMLInputElement>>('box');
  protected readonly text = signal('');

  constructor() {
    effect(() => {
      const external = this.value() ?? '';
      const element = this.box().nativeElement;
      if (document.activeElement !== element || external === '') {
        this.text.set(external);
        element.value = external;
      }
    });
  }

  protected onInput(value: string): void {
    this.text.set(value);
    this.valueChange.emit(value);
  }
}

export interface FilterOption {
  value: string;
  label: string;
}

/**
 * 32px filter select with no visible label: the trigger reads "Status: All" / "Status: Open". `undefined` means
 * "All". The label is also the accessible name.
 */
@Component({
  selector: 'app-filter-select',
  imports: [MatFormFieldModule, MatSelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'filter-select' },
  template: `
    <mat-form-field class="filter-field" subscriptSizing="dynamic">
      <mat-select
        [value]="value()"
        [aria-label]="label()"
        [placeholder]="label() + ': ' + allLabel()"
        [panelWidth]="null"
        (selectionChange)="valueChange.emit($event.value ?? undefined)"
      >
        <mat-select-trigger>{{ label() }}: {{ selectedLabel() }}</mat-select-trigger>
        <mat-option [value]="undefined">{{ allLabel() }}</mat-option>
        @for (option of options(); track option.value) {
          <mat-option [value]="option.value">{{ option.label }}</mat-option>
        }
      </mat-select>
    </mat-form-field>
  `,
})
export class FilterSelect {
  readonly label = input.required<string>();
  readonly options = input.required<readonly FilterOption[]>();
  readonly value = input<string | number | undefined>();
  readonly allLabel = input('All');
  readonly valueChange = output<string | undefined>();

  protected selectedLabel(): string {
    const value = this.value();
    return this.options().find((o) => o.value === value)?.label ?? this.allLabel();
  }
}
