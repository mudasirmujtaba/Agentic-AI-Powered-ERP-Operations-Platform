import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { WriteAccess } from '../../../../core/auth/roles';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { HasRoleDirective } from '../../../../shared/directives/has-role.directive';
import { PURCHASE_ORDER_STATUSES, statusOptions, StatusStyle } from '../../../../shared/models/statuses';
import { PurchaseOrdersApi } from '../../data/purchasing.api';
import { PurchaseOrderListItem } from '../../data/purchasing.models';

@Component({
  selector: 'app-purchase-order-list',
  imports: [
    CurrencyPipe,
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
    HasRoleDirective,
  ],
  templateUrl: './purchase-order-list.html',
})
export class PurchaseOrderList {
  private readonly api = inject(PurchaseOrdersApi);
  private readonly router = inject(Router);

  protected readonly writeAccess = WriteAccess.purchaseOrders;
  protected readonly statuses: Record<string, StatusStyle> = PURCHASE_ORDER_STATUSES;
  protected readonly statusOptions = statusOptions(PURCHASE_ORDER_STATUSES);
  protected readonly columns = ['poNumber', 'supplierName', 'orderDateUtc', 'expectedDeliveryDateUtc', 'warehouseCode', 'status', 'totalAmount'];
  protected readonly initialStatus = inject(ActivatedRoute).snapshot.queryParamMap.get('status') ?? undefined;

  protected readonly list = createPagedList((query) => this.api.list(query), {
    sortBy: 'poNumber',
    sortDirection: 'desc',
    status: this.initialStatus,
  });

  protected open(po: PurchaseOrderListItem): void {
    this.router.navigate(['/purchasing', po.id]);
  }
}
