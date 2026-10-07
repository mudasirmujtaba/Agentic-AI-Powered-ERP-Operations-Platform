import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';

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
import { SALES_ORDER_STATUSES } from '../../../../shared/models/statuses';
import { CustomersApi } from '../../../customers/data/customers.api';
import { InvoicesApi } from '../../../finance/data/finance.api';
import { WarehousesApi } from '../../../inventory/warehouses/data/warehouses.api';
import { ProductsApi } from '../../../products/data/catalog.api';
import { SalesOrdersApi } from '../../data/sales.api';
import { SalesOrder, SaveSalesOrderRequest } from '../../data/sales.models';
import { Icon } from '../../../../shared/components/icon/icon';

@Component({
  selector: 'app-sales-order-page',
  imports: [Icon, 
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    LineItemsEditor,
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './sales-order-page.html',
})
export class SalesOrderPage implements OnInit {
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(SalesOrdersApi);
  private readonly invoicesApi = inject(InvoicesApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly statuses = SALES_ORDER_STATUSES;
  protected readonly firstError = firstError;

  protected readonly order = signal<SalesOrder | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly showShipForm = signal(false);

  protected readonly canSell = computed(() => this.auth.hasAnyRole(WriteAccess.salesOrders));
  protected readonly canFulfil = computed(() => this.auth.hasAnyRole(WriteAccess.fulfilment));
  protected readonly canInvoice = computed(() => this.auth.hasAnyRole(WriteAccess.finance));
  protected readonly isEditable = computed(() => this.canSell() && (!this.id() || this.order()?.status === 'Draft'));
  protected readonly status = computed(() => this.order()?.status);

  protected readonly customers = toSignal(
    inject(CustomersApi)
      .list({ page: 1, pageSize: 100, sortBy: 'name' })
      .pipe(map((r) => r.items.filter((c) => c.status !== 'Inactive')), catchError(() => of([]))),
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
        map((r) => r.items.filter((p) => p.isActive).map((p) => ({ id: p.id, code: p.code, name: p.name, defaultPrice: p.unitPrice }))),
        catchError(() => of([])),
      ),
    { initialValue: [] },
  );

  protected readonly form = this.fb.group({
    customerId: ['', Validators.required],
    warehouseId: ['', Validators.required],
    requiredDate: [''],
    notes: ['', Validators.maxLength(1000)],
    lines: this.fb.array<LineItemForm>([createLineItem(this.fb)]),
  });

  protected readonly shipForm = this.fb.group({ carrier: [''], trackingNumber: [''] });

  protected get lines(): FormArray<LineItemForm> {
    return this.form.controls.lines;
  }

  ngOnInit(): void {
    const id = this.id();
    if (!id) {
      return;
    }
    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (order) => {
        this.load(order);
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
    const request: SaveSalesOrderRequest = {
      customerId: value.customerId,
      warehouseId: value.warehouseId,
      requiredDateUtc: dateInputToUtc(value.requiredDate),
      notes: value.notes.trim() || null,
      lines: value.lines.map((l) => ({ productId: l.productId, quantity: l.quantity, unitPrice: l.unitPrice })),
    };

    const id = this.id();
    this.busy.set(true);
    this.formError.set(null);
    (id ? this.api.update(id, request) : this.api.create(request)).subscribe({
      next: (order) => {
        this.busy.set(false);
        this.snackBar.open(`Order ${order.orderNumber} saved.`, 'Dismiss', { duration: 3000 });
        if (id) {
          this.load(order);
        } else {
          this.router.navigate(['/sales', order.id]);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.formError.set(handleFormError(this.form, err));
      },
    });
  }

  protected confirm(): void {
    if (this.form.dirty) {
      this.formError.set('Save your changes before confirming the order.');
      return;
    }
    this.run('confirm', 'Order confirmed and stock reserved.');
  }

  protected startProcessing(): void {
    this.run('start-processing', 'Order is now processing.');
  }

  protected ship(): void {
    const value = this.shipForm.getRawValue();
    this.run('ship', 'Order shipped and stock deducted.', {
      carrier: value.carrier.trim() || null,
      trackingNumber: value.trackingNumber.trim() || null,
    });
  }

  protected deliver(): void {
    this.run('deliver', 'Order marked as delivered.');
  }

  protected cancel(): void {
    this.run('cancel', 'Order cancelled.');
  }

  protected createInvoice(): void {
    const order = this.order();
    if (!order) return;
    this.busy.set(true);
    this.invoicesApi.create({ salesOrderId: order.id }).subscribe({
      next: (invoice) => {
        this.busy.set(false);
        this.snackBar.open(`Draft invoice ${invoice.invoiceNumber} created.`, 'Dismiss', { duration: 3000 });
        this.router.navigate(['/finance/invoices', invoice.id]);
      },
      error: (err) => this.fail(err),
    });
  }

  private run(action: string, message: string, body: unknown = {}): void {
    const order = this.order();
    if (!order) return;
    this.busy.set(true);
    this.formError.set(null);
    this.handle(this.api.action(order.id, action, body), message);
  }

  private handle(request$: Observable<SalesOrder>, message: string): void {
    request$.subscribe({
      next: (order) => {
        this.busy.set(false);
        this.showShipForm.set(false);
        this.load(order);
        this.snackBar.open(message, 'Dismiss', { duration: 3000 });
      },
      error: (err) => this.fail(err),
    });
  }

  private fail(err: unknown): void {
    this.busy.set(false);
    this.formError.set(describeApiError(err));
  }

  private load(order: SalesOrder): void {
    this.order.set(order);
    this.lines.clear();
    order.lines.forEach((l) => this.lines.push(createLineItem(this.fb, l)));
    this.form.patchValue({
      customerId: order.customerId,
      warehouseId: order.warehouseId,
      requiredDate: utcToDateInput(order.requiredDateUtc),
      notes: order.notes ?? '',
    });
    this.form.markAsPristine();
    if (this.isEditable()) {
      this.form.enable();
    } else {
      this.form.disable();
    }
  }
}
