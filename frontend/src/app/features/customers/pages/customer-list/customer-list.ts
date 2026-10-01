import { CurrencyPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';

import { WriteAccess } from '../../../../core/auth/roles';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { BadgeTone, StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { HasRoleDirective } from '../../../../shared/directives/has-role.directive';
import { CustomersApi } from '../../data/customers.api';
import { CUSTOMER_STATUSES, CustomerListItem, CustomerStatus } from '../../data/customers.models';

const STATUS_TONES: Record<CustomerStatus, BadgeTone> = { Active: 'success', OnHold: 'warning', Inactive: 'neutral' };

@Component({
  selector: 'app-customer-list',
  imports: [
    CurrencyPipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
    HasRoleDirective,
  ],
  templateUrl: './customer-list.html',
})
export class CustomerList {
  private readonly api = inject(CustomersApi);
  private readonly router = inject(Router);

  protected readonly writeAccess = WriteAccess.customers;
  protected readonly columns = ['code', 'name', 'contactName', 'email', 'creditLimit', 'status'];
  protected readonly list = createPagedList((query) => this.api.list(query), { sortBy: 'name' });

  protected statusTone(status: CustomerStatus): BadgeTone {
    return STATUS_TONES[status];
  }

  protected statusLabel(status: CustomerStatus): string {
    return CUSTOMER_STATUSES.find((s) => s.value === status)?.label ?? status;
  }

  protected open(customer: CustomerListItem): void {
    this.router.navigate(['/customers', customer.id]);
  }
}
