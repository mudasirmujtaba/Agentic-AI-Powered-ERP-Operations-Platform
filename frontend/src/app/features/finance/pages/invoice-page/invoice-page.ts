import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError, handleFormError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { dateInputToUtc } from '../../../../shared/forms/dates';
import { firstError } from '../../../../shared/forms/form-errors';
import { INVOICE_STATUSES, OVERDUE_STATUS } from '../../../../shared/models/statuses';
import { InvoicesApi } from '../../data/finance.api';
import { Invoice, PAYMENT_METHODS, PaymentMethod } from '../../data/finance.models';

@Component({
  selector: 'app-invoice-page',
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
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './invoice-page.html',
})
export class InvoicePage implements OnInit {
  readonly id = input.required<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(InvoicesApi);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly statuses = INVOICE_STATUSES;
  protected readonly overdue = OVERDUE_STATUS;
  protected readonly methods = PAYMENT_METHODS;
  protected readonly firstError = firstError;

  protected readonly invoice = signal<Invoice | null>(null);
  protected readonly busy = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly showPaymentForm = signal(false);
  protected readonly canManage = computed(() => this.auth.hasAnyRole(WriteAccess.finance));
  protected readonly methodLabel = (method: PaymentMethod) => this.methods.find((m) => m.value === method)?.label ?? method;

  protected readonly paymentForm = this.fb.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paidAt: [new Date().toISOString().substring(0, 10)],
    method: ['BankTransfer' as PaymentMethod, Validators.required],
    reference: ['', Validators.maxLength(100)],
  });

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (invoice) => this.load(invoice),
      error: (err) => this.fail(err),
    });
  }

  protected issue(): void {
    this.run('issue', {}, 'Invoice issued.');
  }

  protected cancel(): void {
    this.run('cancel', {}, 'Invoice cancelled.');
  }

  protected openPaymentForm(): void {
    this.paymentForm.patchValue({ amount: this.invoice()?.balance ?? 0 });
    this.showPaymentForm.set(true);
  }

  protected recordPayment(): void {
    if (this.paymentForm.invalid) {
      this.paymentForm.markAllAsTouched();
      return;
    }
    const value = this.paymentForm.getRawValue();
    this.run(
      'payments',
      {
        amount: value.amount,
        paidAtUtc: dateInputToUtc(value.paidAt),
        method: value.method,
        reference: value.reference.trim() || null,
      },
      'Payment recorded.',
    );
  }

  private run(action: string, body: unknown, message: string): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.action(this.id(), action, body).subscribe({
      next: (invoice) => {
        this.load(invoice);
        this.showPaymentForm.set(false);
        this.snackBar.open(message, 'Dismiss', { duration: 3000 });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(handleFormError(this.paymentForm, err) ?? 'Please fix the highlighted fields.');
      },
    });
  }

  private load(invoice: Invoice): void {
    this.invoice.set(invoice);
    this.busy.set(false);
  }

  private fail(err: unknown): void {
    this.busy.set(false);
    this.error.set(describeApiError(err));
  }
}
