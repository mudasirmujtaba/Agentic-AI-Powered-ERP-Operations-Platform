import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError, handleFormError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { firstError } from '../../../../shared/forms/form-errors';
import { SuppliersApi } from '../../data/suppliers.api';
import { SaveSupplierRequest } from '../../data/suppliers.models';
import { Icon } from '../../../../shared/components/icon/icon';

@Component({
  selector: 'app-supplier-form',
  imports: [Icon, 
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    PageHeader,
  ],
  templateUrl: './supplier-form.html',
})
export class SupplierForm implements OnInit {
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(SuppliersApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly firstError = firstError;
  protected readonly isNew = computed(() => !this.id());
  protected readonly canEdit = computed(() => this.auth.hasAnyRole(WriteAccess.suppliers));
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly title = signal('New supplier');

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(30)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    contactName: ['', Validators.maxLength(150)],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    phone: ['', Validators.maxLength(50)],
    paymentTermsDays: [30, [Validators.required, Validators.min(0), Validators.max(365)]],
    averageLeadTimeDays: [7, [Validators.required, Validators.min(0), Validators.max(365)]],
    isActive: [true],
  });

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
      next: (supplier) => {
        this.title.set(`${supplier.code} · ${supplier.name}`);
        this.form.patchValue({
          ...supplier,
          contactName: supplier.contactName ?? '',
          email: supplier.email ?? '',
          phone: supplier.phone ?? '',
        });
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
    const optional = (text: string) => (text.trim() ? text.trim() : null);
    const request: SaveSupplierRequest = {
      ...value,
      code: value.code.trim(),
      name: value.name.trim(),
      contactName: optional(value.contactName),
      email: optional(value.email),
      phone: optional(value.phone),
    };

    const id = this.id();
    (id ? this.api.update(id, request) : this.api.create(request)).subscribe({
      next: (supplier) => {
        this.saving.set(false);
        this.snackBar.open(`Supplier ${supplier.code} saved.`, 'Dismiss', { duration: 3000 });
        this.router.navigate(['/suppliers']);
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(handleFormError(this.form, err, 'code'));
      },
    });
  }
}
