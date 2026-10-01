import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError, handleFormError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { firstError } from '../../../../shared/forms/form-errors';
import { SuppliersApi } from '../../../suppliers/data/suppliers.api';
import { CategoriesApi, ProductsApi } from '../../data/catalog.api';
import { SaveProductRequest } from '../../data/catalog.models';

function safetyStockWithinReorderPoint(group: AbstractControl): ValidationErrors | null {
  const reorderPoint = group.get('reorderPoint')?.value;
  const safetyStock = group.get('safetyStock')?.value;
  return typeof reorderPoint === 'number' && typeof safetyStock === 'number' && safetyStock > reorderPoint
    ? { safetyStockExceedsReorderPoint: true }
    : null;
}

@Component({
  selector: 'app-product-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    PageHeader,
  ],
  templateUrl: './product-form.html',
})
export class ProductForm implements OnInit {
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(ProductsApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly firstError = firstError;
  protected readonly isNew = computed(() => !this.id());
  protected readonly canEdit = computed(() => this.auth.hasAnyRole(WriteAccess.catalog));
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly title = signal('New product');

  protected readonly categories = toSignal(
    inject(CategoriesApi)
      .list({ page: 1, pageSize: 100, sortBy: 'name' })
      .pipe(map((r) => r.items), catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly suppliers = toSignal(
    inject(SuppliersApi)
      .list({ page: 1, pageSize: 100, sortBy: 'name' })
      .pipe(map((r) => r.items), catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly form = this.fb.group(
    {
      code: ['', [Validators.required, Validators.maxLength(30)]],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', Validators.maxLength(1000)],
      categoryId: ['', Validators.required],
      primarySupplierId: [null as string | null],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      cost: [0, [Validators.required, Validators.min(0)]],
      reorderPoint: [0, [Validators.required, Validators.min(0)]],
      safetyStock: [0, [Validators.required, Validators.min(0)]],
      isActive: [true],
    },
    { validators: safetyStockWithinReorderPoint },
  );

  ngOnInit(): void {
    if (!this.canEdit()) {
      this.form.disable();
    }

    const id = this.id();
    if (!id) {
      return;
    }

    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (product) => {
        this.title.set(`${product.code} · ${product.name}`);
        this.form.patchValue({ ...product, description: product.description ?? '' });
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

    this.saving.set(true);
    this.formError.set(null);

    const value = this.form.getRawValue();
    const request: SaveProductRequest = {
      ...value,
      code: value.code.trim(),
      name: value.name.trim(),
      description: value.description.trim() || null,
    };

    const id = this.id();
    (id ? this.api.update(id, request) : this.api.create(request)).subscribe({
      next: (product) => {
        this.saving.set(false);
        this.snackBar.open(`Product ${product.code} saved.`, 'Dismiss', { duration: 3000 });
        this.router.navigate(['/products']);
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(handleFormError(this.form, err, 'code'));
      },
    });
  }
}
