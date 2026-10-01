import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError, handleFormError } from '../../../../core/http/api-error';
import {
  LineItemForm,
  LineItemsEditor,
  LineProductOption,
  createLineItem,
} from '../../../../shared/components/line-items-editor/line-items-editor';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { dateInputToUtc, utcToDateInput } from '../../../../shared/forms/dates';
import { firstError } from '../../../../shared/forms/form-errors';
import { PURCHASE_ORDER_STATUSES } from '../../../../shared/models/statuses';
import { WarehousesApi } from '../../../inventory/warehouses/data/warehouses.api';
import { ProductsApi } from '../../../products/data/catalog.api';
import { SuppliersApi } from '../../../suppliers/data/suppliers.api';
import { PurchaseOrdersApi } from '../../data/purchasing.api';
import { PurchaseOrder, SavePurchaseOrderRequest } from '../../data/purchasing.models';

@Component({
  selector: 'app-purchase-order-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    LineItemsEditor,
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './purchase-order-page.html',
})
export class PurchaseOrderPage implements OnInit {
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(PurchaseOrdersApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly statuses = PURCHASE_ORDER_STATUSES;
  protected readonly firstError = firstError;

  protected readonly po = signal<PurchaseOrder | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly showRejectForm = signal(false);
  protected readonly showReceiveForm = signal(false);

  protected readonly canPurchase = computed(() => this.auth.hasAnyRole(WriteAccess.purchaseOrders));
  protected readonly canApprove = computed(() => this.auth.hasAnyRole(WriteAccess.approvePurchaseOrders));
  protected readonly canReceive = computed(() => this.auth.hasAnyRole(WriteAccess.receiveGoods));
  protected readonly isEditable = computed(() => this.canPurchase() && (!this.id() || this.po()?.status === 'Draft'));

  protected readonly suppliers = toSignal(
    inject(SuppliersApi)
      .list({ page: 1, pageSize: 100, sortBy: 'name' })
      .pipe(map((r) => r.items.filter((s) => s.isActive)), catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly warehouses = toSignal(
    inject(WarehousesApi)
      .list({ page: 1, pageSize: 100, sortBy: 'code' })
      .pipe(map((r) => r.items.filter((w) => w.isActive)), catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly products = toSignal<LineProductOption[], LineProductOption[]>(
    inject(ProductsApi)
      .list({ page: 1, pageSize: 100, sortBy: 'code' })
      .pipe(
        map((r) => r.items.map((p) => ({ id: p.id, code: p.code, name: p.name, defaultPrice: p.cost }))),
        catchError(() => of([])),
      ),
    { initialValue: [] },
  );

  protected readonly form = this.fb.group({
    supplierId: ['', Validators.required],
    warehouseId: ['', Validators.required],
    expectedDate: [''],
    notes: ['', Validators.maxLength(1000)],
    lines: this.fb.array<LineItemForm>([createLineItem(this.fb)]),
  });

  protected readonly rejectReason = this.fb.control('', [Validators.required, Validators.maxLength(500)]);
  protected readonly receiveQuantities = this.fb.array<FormControl<number>>([]);

  protected get lines(): FormArray<LineItemForm> {
    return this.form.controls.lines;
  }

  ngOnInit(): void {
    const id = this.id();
    if (!id) return;
    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (po) => {
        this.load(po);
        this.loading.set(false);
      },
      error: (err) => {
        this.formError.set(describeApiError(err));
        this.loading.set(false);
      },
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const request: SavePurchaseOrderRequest = {
      supplierId: value.supplierId,
      warehouseId: value.warehouseId,
      expectedDeliveryDateUtc: dateInputToUtc(value.expectedDate),
      notes: value.notes.trim() || null,
      lines: value.lines.map((l) => ({ productId: l.productId, quantity: l.quantity, unitCost: l.unitPrice })),
    };

    const id = this.id();
    this.busy.set(true);
    this.formError.set(null);
    (id ? this.api.update(id, request) : this.api.create(request)).subscribe({
      next: (po) => {
        this.busy.set(false);
        this.snackBar.open(`${po.poNumber} saved.`, 'Dismiss', { duration: 3000 });
        if (id) this.load(po);
        else this.router.navigate(['/purchasing', po.id]);
      },
      error: (err) => {
        this.busy.set(false);
        this.formError.set(handleFormError(this.form, err));
      },
    });
  }

  protected submit(): void {
    if (this.form.dirty) {
      this.formError.set('Save your changes before submitting.');
      return;
    }
    this.run('submit', {}, (po) => (po.status === 'PendingApproval' ? 'Submitted for manager approval.' : 'Approved automatically (under the threshold).'));
  }

  protected approve(): void {
    this.run('approve', {}, () => 'Purchase order approved.');
  }

  protected reject(): void {
    if (this.rejectReason.invalid) {
      this.rejectReason.markAsTouched();
      return;
    }
    this.run('reject', { reason: this.rejectReason.value.trim() }, () => 'Returned to draft with your reason.');
  }

  protected markOrdered(): void {
    this.run('mark-ordered', {}, () => 'Marked as sent to the supplier.');
  }

  protected receive(): void {
    const po = this.po();
    if (!po || this.receiveQuantities.invalid) {
      this.receiveQuantities.markAllAsTouched();
      return;
    }
    const lines = po.lines.map((line, i) => ({ lineId: line.id, quantity: this.receiveQuantities.at(i).value || 0 }));
    this.run('receive', { lines }, () => 'Goods received into stock.');
  }

  protected cancel(): void {
    this.run('cancel', {}, () => 'Purchase order cancelled.');
  }

  private run(action: string, body: unknown, message: (po: PurchaseOrder) => string): void {
    const po = this.po();
    if (!po) return;
    this.busy.set(true);
    this.formError.set(null);
    this.api.action(po.id, action, body).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.showRejectForm.set(false);
        this.showReceiveForm.set(false);
        this.load(updated);
        this.snackBar.open(message(updated), 'Dismiss', { duration: 3500 });
      },
      error: (err) => {
        this.busy.set(false);
        this.formError.set(describeApiError(err));
      },
    });
  }

  private load(po: PurchaseOrder): void {
    this.po.set(po);
    this.lines.clear();
    po.lines.forEach((l) => this.lines.push(createLineItem(this.fb, { productId: l.productId, quantity: l.quantity, unitPrice: l.unitCost })));
    this.form.patchValue({
      supplierId: po.supplierId,
      warehouseId: po.warehouseId,
      expectedDate: utcToDateInput(po.expectedDeliveryDateUtc),
      notes: po.notes ?? '',
    });
    this.form.markAsPristine();
    if (this.isEditable()) this.form.enable();
    else this.form.disable();

    this.receiveQuantities.clear();
    po.lines.forEach((l) =>
      this.receiveQuantities.push(this.fb.control(l.quantityRemaining, [Validators.min(0), Validators.max(l.quantityRemaining)])),
    );
    this.rejectReason.reset('');
  }
}
