import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { WriteAccess } from '../../../../core/auth/roles';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { HasRoleDirective } from '../../../../shared/directives/has-role.directive';
import { SALES_ORDER_STATUSES, statusOptions, StatusStyle } from '../../../../shared/models/statuses';
import { SalesOrdersApi } from '../../data/sales.api';
import { SalesOrderListItem } from '../../data/sales.models';
import { Icon } from '../../../../shared/components/icon/icon';
import { FilterBar, FilterSearch, FilterSelect } from '../../../../shared/components/filter-bar/filter-bar';

@Component({
  selector: 'app-sales-order-list',
  imports: [FilterBar, FilterSearch, FilterSelect, Icon, 
    CurrencyPipe,
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatSortModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
    HasRoleDirective,
  ],
  templateUrl: './sales-order-list.html',
})
export class SalesOrderList {
  private readonly api = inject(SalesOrdersApi);
  private readonly router = inject(Router);
  private readonly params = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly writeAccess = WriteAccess.salesOrders;
  protected readonly statuses: Record<string, StatusStyle> = SALES_ORDER_STATUSES;
  protected readonly statusOptions = statusOptions(SALES_ORDER_STATUSES);
  protected readonly columns = ['orderNumber', 'customerName', 'orderDateUtc', 'requiredDateUtc', 'warehouseCode', 'status', 'totalAmount'];

  protected readonly initialStatus = this.params.get('status') ?? undefined;
  protected readonly initialLateOnly = this.params.get('lateOnly') === 'true';

  protected readonly list = createPagedList((query) => this.api.list(query), {
    sortBy: 'orderDateUtc',
    sortDirection: 'desc',
    status: this.initialStatus,
    lateOnly: this.initialLateOnly ? 'true' : undefined,
  });

  protected open(order: SalesOrderListItem): void {
    this.router.navigate(['/sales', order.id]);
  }
}
