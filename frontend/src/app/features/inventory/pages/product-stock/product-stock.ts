import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError, handleFormError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { firstError } from '../../../../shared/forms/form-errors';
import { STOCK_STATUSES } from '../../../../shared/models/statuses';
import { WarehousesApi } from '../../warehouses/data/warehouses.api';
import { InventoryApi } from '../../data/inventory.api';
import { ProductStock } from '../../data/inventory.models';

type Panel = 'adjust' | 'transfer' | null;

@Component({
  selector: 'app-product-stock',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './product-stock.html',
})
export class ProductStockPage implements OnInit {
  readonly productId = input.required<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(InventoryApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly statuses = STOCK_STATUSES;
  protected readonly firstError = firstError;
  protected readonly stock = signal<ProductStock | null>(null);
  protected readonly busy = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly panel = signal<Panel>(null);
  protected readonly canManage = computed(() => this.auth.hasAnyRole(WriteAccess.inventory));

  protected readonly warehouses = toSignal(
    inject(WarehousesApi)
      .list({ page: 1, pageSize: 100, sortBy: 'code' })
      .pipe(map((r) => r.items.filter((w) => w.isActive)), catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly adjustForm = this.fb.group({
    warehouseId: ['', Validators.required],
    reason: ['Adjustment' as 'Adjustment' | 'Damaged' | 'Return', Validators.required],
    quantity: [0, Validators.required],
    notes: ['', Validators.maxLength(500)],
  });

  protected readonly transferForm = this.fb.group({
    fromWarehouseId: ['', Validators.required],
    toWarehouseId: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(1)]],
    notes: ['', Validators.maxLength(500)],
  });

  ngOnInit(): void {
    this.api.productStock(this.productId()).subscribe({
      next: (stock) => this.load(stock),
      error: (err) => {
        this.busy.set(false);
        this.error.set(describeApiError(err));
      },
    });
  }

  protected toggle(panel: Panel): void {
    this.panel.set(this.panel() === panel ? null : panel);
  }

  protected adjust(): void {
    if (this.adjustForm.invalid) {
      this.adjustForm.markAllAsTouched();
      return;
    }
    const value = this.adjustForm.getRawValue();
    this.submit(
      this.api.adjust({ productId: this.productId(), ...value, notes: value.notes.trim() || null }),
      'Stock adjusted.',
      this.adjustForm,
    );
  }

  protected transfer(): void {
    if (this.transferForm.invalid) {
      this.transferForm.markAllAsTouched();
      return;
    }
    const value = this.transferForm.getRawValue();
    this.submit(
      this.api.transfer({ productId: this.productId(), ...value, notes: value.notes.trim() || null }),
      'Stock transferred.',
      this.transferForm,
    );
  }

  private submit(request$: Observable<ProductStock>, message: string, form: FormGroup): void {
    this.busy.set(true);
    this.error.set(null);
    request$.subscribe({
      next: (stock) => {
        this.load(stock);
        this.panel.set(null);
        form.reset();
        this.snackBar.open(message, 'Dismiss', { duration: 3000 });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(handleFormError(form, err));
      },
    });
  }

  private load(stock: ProductStock): void {
    this.stock.set(stock);
    this.busy.set(false);
  }
}
