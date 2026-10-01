import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, distinctUntilChanged, map, of, switchMap } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError, handleFormError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { firstError } from '../../../../shared/forms/form-errors';
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  TICKET_STATUSES,
  TicketCategory,
  TicketPriority,
  TicketStatus,
  statusOptions,
} from '../../../../shared/models/statuses';
import { MarkdownLitePipe } from '../../../../shared/pipes/markdown-lite.pipe';
import { CustomersApi } from '../../../customers/data/customers.api';
import { ProductsApi } from '../../../products/data/catalog.api';
import { SalesOrdersApi } from '../../../sales/data/sales.api';
import { TicketsApi } from '../../data/tickets.api';
import { SaveTicketRequest, Ticket } from '../../data/tickets.models';

@Component({
  selector: 'app-ticket-page',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MarkdownLitePipe,
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './ticket-page.html',
  styles: `
    :host ::ng-deep .answer p { margin: 0 0 0.5rem; }
    :host ::ng-deep .answer ul, :host ::ng-deep .answer ol { margin: 0 0 0.5rem; padding-left: 1.25rem; }
    :host ::ng-deep .answer ul { list-style: disc; }
    :host ::ng-deep .answer ol { list-style: decimal; }
  `,
})
export class TicketPage implements OnInit {
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(TicketsApi);
  private readonly ordersApi = inject(SalesOrdersApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly statuses = TICKET_STATUSES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly categories = TICKET_CATEGORIES;
  protected readonly priorityOptions = statusOptions(TICKET_PRIORITIES);
  protected readonly categoryOptions = Object.entries(TICKET_CATEGORIES).map(([value, label]) => ({
    value: value as TicketCategory,
    label,
  }));
  protected readonly firstError = firstError;

  protected readonly ticket = signal<Ticket | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly summarizing = signal(false);
  protected readonly formError = signal<string | null>(null);
  /** Set while the resolution box is open for a Resolve action. */
  protected readonly resolving = signal(false);

  protected readonly canManage = computed(() => this.auth.hasAnyRole(WriteAccess.tickets));
  protected readonly isEditable = computed(() => this.canManage() && this.ticket()?.status !== 'Closed');

  protected readonly customers = toSignal(
    inject(CustomersApi)
      .list({ page: 1, pageSize: 100, sortBy: 'name' })
      .pipe(map((r) => r.items.filter((c) => c.status !== 'Inactive')), catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly products = toSignal(
    inject(ProductsApi)
      .list({ page: 1, pageSize: 100, sortBy: 'code' })
      .pipe(map((r) => r.items), catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly assignees = toSignal(this.api.assignees().pipe(catchError(() => of([]))), { initialValue: [] });

  protected readonly form = this.fb.group({
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    customerId: ['', Validators.required],
    salesOrderId: [null as string | null],
    productId: [null as string | null],
    category: ['General' as TicketCategory, Validators.required],
    priority: ['Medium' as TicketPriority, Validators.required],
    assignedToUserId: [null as string | null],
  });

  protected readonly commentForm = this.fb.group({
    body: ['', [Validators.required, Validators.maxLength(4000)]],
    isInternal: [false],
  });

  protected readonly resolutionControl = this.fb.control('', [Validators.required, Validators.maxLength(2000)]);

  /** The selected customer's recent orders, for linking the ticket to one. */
  protected readonly customerOrders = toSignal(
    this.form.controls.customerId.valueChanges.pipe(
      distinctUntilChanged(),
      switchMap((customerId) =>
        customerId
          ? this.ordersApi
              .list({ page: 1, pageSize: 50, customerId, sortBy: 'orderDateUtc', sortDirection: 'desc' })
              .pipe(map((r) => r.items), catchError(() => of([])))
          : of([]),
      ),
    ),
    { initialValue: [] },
  );

  constructor() {
    // Changing customer invalidates an order link that belonged to the previous one.
    this.form.controls.customerId.valueChanges.pipe(distinctUntilChanged(), takeUntilDestroyed()).subscribe(() => {
      const orderId = this.form.controls.salesOrderId.value;
      if (orderId && orderId !== this.ticket()?.salesOrderId) this.form.controls.salesOrderId.setValue(null);
    });
  }

  ngOnInit(): void {
    const id = this.id();
    if (!id) return;
    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (ticket) => {
        this.load(ticket);
        this.loading.set(false);
      },
      error: (err) => {
        this.formError.set(describeApiError(err));
        this.loading.set(false);
      },
    });
  }

  protected actionLabel(target: TicketStatus): string {
    const current = this.ticket()?.status;
    if (target === 'InProgress') return current === 'Resolved' || current === 'Closed' ? 'Reopen' : 'Start work';
    if (target === 'WaitingOnCustomer') return 'Waiting on customer';
    if (target === 'Resolved') return 'Resolve';
    return 'Close';
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const request: SaveTicketRequest = {
      ...value,
      subject: value.subject.trim(),
      description: value.description.trim(),
    };

    const id = this.id();
    this.busy.set(true);
    this.formError.set(null);
    (id ? this.api.update(id, request) : this.api.create(request)).subscribe({
      next: (ticket) => {
        this.busy.set(false);
        this.snackBar.open(`Ticket ${ticket.ticketNumber} saved.`, 'Dismiss', { duration: 3000 });
        if (id) {
          this.load(ticket);
        } else {
          this.router.navigate(['/tickets', ticket.id]);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.formError.set(handleFormError(this.form, err));
      },
    });
  }

  protected changeStatus(target: TicketStatus): void {
    if (target === 'Resolved') {
      this.resolving.set(true);
      return;
    }
    this.run((id) => this.api.changeStatus(id, target), `Ticket moved to ${this.statuses[target].label.toLowerCase()}.`);
  }

  protected confirmResolve(): void {
    if (this.resolutionControl.invalid) {
      this.resolutionControl.markAsTouched();
      return;
    }
    const resolution = this.resolutionControl.value.trim();
    this.run((id) => this.api.changeStatus(id, 'Resolved', resolution), 'Ticket resolved.');
  }

  protected addComment(): void {
    if (this.commentForm.invalid || !this.commentForm.value.body?.trim()) {
      this.commentForm.markAllAsTouched();
      return;
    }
    const { body, isInternal } = this.commentForm.getRawValue();
    this.run((id) => this.api.addComment(id, body.trim(), isInternal), isInternal ? 'Internal note added.' : 'Comment added.', () =>
      this.commentForm.reset({ body: '', isInternal: false }),
    );
  }

  protected summarize(): void {
    const ticket = this.ticket();
    if (!ticket) return;
    this.summarizing.set(true);
    this.formError.set(null);
    this.api.summarize(ticket.id).subscribe({
      next: (updated) => {
        this.summarizing.set(false);
        this.ticket.set(updated);
      },
      error: (err) => {
        this.summarizing.set(false);
        this.formError.set(describeApiError(err));
      },
    });
  }

  private run(request: (id: string) => Observable<Ticket>, message: string, onDone?: () => void): void {
    const ticket = this.ticket();
    if (!ticket) return;
    this.busy.set(true);
    this.formError.set(null);
    request(ticket.id).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.resolving.set(false);
        this.resolutionControl.reset('');
        this.load(updated);
        onDone?.();
        this.snackBar.open(message, 'Dismiss', { duration: 3000 });
      },
      error: (err) => {
        this.busy.set(false);
        this.formError.set(describeApiError(err));
      },
    });
  }

  private load(ticket: Ticket): void {
    this.ticket.set(ticket);
    this.form.patchValue({
      subject: ticket.subject,
      description: ticket.description,
      customerId: ticket.customerId,
      salesOrderId: ticket.salesOrderId,
      productId: ticket.productId,
      category: ticket.category,
      priority: ticket.priority,
      assignedToUserId: ticket.assignedToUserId,
    });
    this.form.markAsPristine();
    if (this.isEditable()) {
      this.form.enable();
    } else {
      this.form.disable();
    }
  }
}
