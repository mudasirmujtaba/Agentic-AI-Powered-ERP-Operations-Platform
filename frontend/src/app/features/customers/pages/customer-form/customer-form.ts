import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError, handleFormError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { firstError } from '../../../../shared/forms/form-errors';
import { CustomersApi } from '../../data/customers.api';
import {
  AddressType,
  CUSTOMER_STATUSES,
  Customer,
  CustomerAddress,
  CustomerStatus,
  SaveCustomerRequest,
} from '../../data/customers.models';

type AddressForm = FormGroup<{
  type: FormControl<AddressType>;
  line1: FormControl<string>;
  line2: FormControl<string>;
  city: FormControl<string>;
  state: FormControl<string>;
  postalCode: FormControl<string>;
  country: FormControl<string>;
  isDefault: FormControl<boolean>;
}>;

@Component({
  selector: 'app-customer-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    PageHeader,
  ],
  templateUrl: './customer-form.html',
})
export class CustomerForm implements OnInit {
  /** Bound from the `:id` route param; absent on `/customers/new`. */
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CustomersApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly statuses = CUSTOMER_STATUSES;
  protected readonly addressTypes: AddressType[] = ['Billing', 'Shipping'];
  protected readonly firstError = firstError;

  protected readonly isNew = computed(() => !this.id());
  protected readonly canEdit = computed(() => this.auth.hasAnyRole(WriteAccess.customers));
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly title = signal('New customer');

  protected readonly form = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(30)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    contactName: ['', Validators.maxLength(150)],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    phone: ['', Validators.maxLength(50)],
    creditLimit: [0, [Validators.required, Validators.min(0)]],
    paymentTermsDays: [30, [Validators.required, Validators.min(0), Validators.max(365)]],
    status: ['Active' as CustomerStatus, Validators.required],
    addresses: this.fb.array<AddressForm>([]),
  });

  protected get addresses(): FormArray<AddressForm> {
    return this.form.controls.addresses;
  }

  ngOnInit(): void {
    const id = this.id();
    if (id) {
      this.loading.set(true);
      this.api.get(id).subscribe({
        next: (customer) => {
          this.patch(customer);
          this.loading.set(false);
        },
        error: (err) => {
          this.formError.set(describeApiError(err));
          this.loading.set(false);
        },
      });
    } else {
      this.addAddress('Billing', true);
    }

    if (!this.canEdit()) {
      this.form.disable();
    }
  }

  protected addAddress(type: AddressType = 'Shipping', isDefault = false): void {
    this.addresses.push(this.createAddress({ type, isDefault }));
  }

  protected removeAddress(index: number): void {
    this.addresses.removeAt(index);
    this.form.markAsDirty();
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    const id = this.id();
    const request = this.toRequest();
    const save$ = id ? this.api.update(id, request) : this.api.create(request);

    save$.subscribe({
      next: (customer) => {
        this.saving.set(false);
        this.snackBar.open(`Customer ${customer.code} saved.`, 'Dismiss', { duration: 3000 });
        this.router.navigate(['/customers']);
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(handleFormError(this.form, err, 'code'));
      },
    });
  }

  private patch(customer: Customer): void {
    this.title.set(`${customer.code} · ${customer.name}`);
    this.addresses.clear();
    customer.addresses.forEach((address) => this.addresses.push(this.createAddress(address)));
    this.form.patchValue({
      code: customer.code,
      name: customer.name,
      contactName: customer.contactName ?? '',
      email: customer.email ?? '',
      phone: customer.phone ?? '',
      creditLimit: customer.creditLimit,
      paymentTermsDays: customer.paymentTermsDays,
      status: customer.status,
    });
    if (!this.canEdit()) {
      this.form.disable();
    }
  }

  private createAddress(address: Partial<CustomerAddress>): AddressForm {
    return this.fb.group({
      type: [address.type ?? ('Billing' as AddressType), Validators.required],
      line1: [address.line1 ?? '', [Validators.required, Validators.maxLength(200)]],
      line2: [address.line2 ?? '', Validators.maxLength(200)],
      city: [address.city ?? '', [Validators.required, Validators.maxLength(100)]],
      state: [address.state ?? '', Validators.maxLength(100)],
      postalCode: [address.postalCode ?? '', Validators.maxLength(20)],
      country: [address.country ?? 'United States', [Validators.required, Validators.maxLength(100)]],
      isDefault: [address.isDefault ?? false],
    });
  }

  private toRequest(): SaveCustomerRequest {
    const value = this.form.getRawValue();
    const optional = (text: string) => (text.trim() ? text.trim() : null);
    return {
      code: value.code.trim(),
      name: value.name.trim(),
      contactName: optional(value.contactName),
      email: optional(value.email),
      phone: optional(value.phone),
      creditLimit: value.creditLimit,
      paymentTermsDays: value.paymentTermsDays,
      status: value.status,
      addresses: value.addresses.map((a) => ({
        type: a.type,
        line1: a.line1.trim(),
        line2: optional(a.line2),
        city: a.city.trim(),
        state: optional(a.state),
        postalCode: optional(a.postalCode),
        country: a.country.trim(),
        isDefault: a.isDefault,
      })),
    };
  }
}
