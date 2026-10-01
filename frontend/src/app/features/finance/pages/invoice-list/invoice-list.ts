import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router } from '@angular/router';

import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { INVOICE_STATUSES, OVERDUE_STATUS, statusOptions, StatusStyle } from '../../../../shared/models/statuses';
import { InvoicesApi } from '../../data/finance.api';
import { InvoiceListItem } from '../../data/finance.models';

@Component({
  selector: 'app-invoice-list',
  imports: [
    CurrencyPipe,
    DatePipe,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSortModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './invoice-list.html',
})
export class InvoiceList {
  private readonly api = inject(InvoicesApi);
  private readonly router = inject(Router);

  protected readonly statuses: Record<string, StatusStyle> = INVOICE_STATUSES;
  protected readonly overdue = OVERDUE_STATUS;
  protected readonly statusOptions = statusOptions(INVOICE_STATUSES);
  protected readonly columns = ['invoiceNumber', 'customerName', 'salesOrderNumber', 'issueDateUtc', 'dueDateUtc', 'status', 'totalAmount', 'balance'];

  protected readonly initialOverdueOnly = inject(ActivatedRoute).snapshot.queryParamMap.get('overdueOnly') === 'true';

  protected readonly list = createPagedList((query) => this.api.list(query), {
    sortBy: 'invoiceNumber',
    sortDirection: 'desc',
    overdueOnly: this.initialOverdueOnly ? 'true' : undefined,
  });

  protected open(invoice: InvoiceListItem): void {
    this.router.navigate(['/finance/invoices', invoice.id]);
  }
}
