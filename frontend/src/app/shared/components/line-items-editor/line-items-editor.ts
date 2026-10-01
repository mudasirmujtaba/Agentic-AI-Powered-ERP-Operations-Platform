import { CurrencyPipe } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { FormArray, FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { firstError } from '../../forms/form-errors';

export interface LineProductOption {
  id: string;
  code: string;
  name: string;
  /** Default price for this document type: unit price for sales, cost for purchasing. */
  defaultPrice: number;
}

export type LineItemForm = FormGroup<{
  productId: FormControl<string>;
  quantity: FormControl<number>;
  unitPrice: FormControl<number>;
}>;

export function createLineItem(
  fb: NonNullableFormBuilder,
  value?: { productId: string; quantity: number; unitPrice: number },
): LineItemForm {
  return fb.group({
    productId: [value?.productId ?? '', Validators.required],
    quantity: [value?.quantity ?? 1, [Validators.required, Validators.min(1)]],
    unitPrice: [value?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
  });
}

/** Editable product / quantity / price rows for sales and purchase orders. */
@Component({
  selector: 'app-line-items-editor',
  imports: [CurrencyPipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <div class="flex flex-col gap-1">
      @for (line of lines().controls; track line; let i = $index) {
        <div [formGroup]="line" class="grid grid-cols-12 items-start gap-2">
          <mat-form-field appearance="outline" class="col-span-12 md:col-span-6">
            <mat-label>Product</mat-label>
            <mat-select formControlName="productId" (selectionChange)="applyDefaultPrice(line, $event.value)">
              @for (product of products(); track product.id) {
                <mat-option [value]="product.id" [disabled]="isChosenElsewhere(product.id, i)">
                  {{ product.code }} · {{ product.name }}
                </mat-option>
              }
            </mat-select>
            <mat-error>{{ firstError(line.controls.productId) }}</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="col-span-4 md:col-span-2">
            <mat-label>Quantity</mat-label>
            <input matInput type="number" min="1" formControlName="quantity" />
            <mat-error>{{ firstError(line.controls.quantity) }}</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline" class="col-span-4 md:col-span-2">
            <mat-label>{{ priceLabel() }}</mat-label>
            <input matInput type="number" min="0" step="0.01" formControlName="unitPrice" />
            <mat-error>{{ firstError(line.controls.unitPrice) }}</mat-error>
          </mat-form-field>

          <div class="col-span-3 pt-4 text-right md:col-span-1">
            {{ (line.controls.quantity.value || 0) * (line.controls.unitPrice.value || 0) | currency: 'USD' }}
          </div>

          <div class="col-span-1 pt-2 text-right">
            <button mat-icon-button type="button" (click)="remove(i)" [disabled]="lines().length === 1" aria-label="Remove line">
              <mat-icon>delete</mat-icon>
            </button>
          </div>
        </div>
      }
    </div>

    <div class="mt-1 flex items-center justify-between">
      <button mat-stroked-button type="button" (click)="add()">
        <mat-icon>add</mat-icon>
        Add line
      </button>
      <span class="text-base font-medium">Total {{ total() | currency: 'USD' }}</span>
    </div>
  `,
})
export class LineItemsEditor {
  readonly lines = input.required<FormArray<LineItemForm>>();
  readonly products = input.required<LineProductOption[]>();
  readonly priceLabel = input('Unit price');

  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly firstError = firstError;

  protected total(): number {
    return this.lines().controls.reduce((sum, l) => sum + (l.controls.quantity.value || 0) * (l.controls.unitPrice.value || 0), 0);
  }

  protected add(): void {
    this.lines().push(createLineItem(this.fb));
  }

  protected remove(index: number): void {
    this.lines().removeAt(index);
    this.lines().markAsDirty();
  }

  protected applyDefaultPrice(line: LineItemForm, productId: string): void {
    const product = this.products().find((p) => p.id === productId);
    if (product) {
      line.controls.unitPrice.setValue(product.defaultPrice);
    }
  }

  protected isChosenElsewhere(productId: string, index: number): boolean {
    return this.lines().controls.some((l, i) => i !== index && l.controls.productId.value === productId);
  }
}
