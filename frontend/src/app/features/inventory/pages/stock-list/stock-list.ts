import { Component, inject, computed } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { STOCK_STATUSES, StatusStyle } from '../../../../shared/models/statuses';
import { WarehousesApi } from '../../warehouses/data/warehouses.api';
import { InventoryApi } from '../../data/inventory.api';
import { StockLevel } from '../../data/inventory.models';
import { Icon } from '../../../../shared/components/icon/icon';
import { FilterBar, FilterSearch, FilterSelect } from '../../../../shared/components/filter-bar/filter-bar';

@Component({
  selector: 'app-stock-list',
  imports: [FilterBar, FilterSearch, FilterSelect, Icon, 
    RouterLink,
    MatButtonModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatSortModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './stock-list.html',
})
export class StockList {
  private readonly api = inject(InventoryApi);
  private readonly router = inject(Router);

  protected readonly statuses: Record<string, StatusStyle> = STOCK_STATUSES;
  protected readonly columns = ['productCode', 'productName', 'categoryName', 'quantityOnHand', 'quantityReserved', 'quantityAvailable', 'reorderPoint', 'status'];
  protected readonly initialLowStockOnly = inject(ActivatedRoute).snapshot.queryParamMap.get('lowStockOnly') === 'true';

  protected readonly list = createPagedList((query) => this.api.stock(query), {
    sortBy: 'productCode',
    lowStockOnly: this.initialLowStockOnly ? 'true' : undefined,
  });

  protected readonly warehouses = toSignal(
    inject(WarehousesApi)
      .list({ page: 1, pageSize: 100, sortBy: 'code' })
      .pipe(map((r) => r.items), catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly warehouseOptions = computed(() =>
    this.warehouses().map((w) => ({ value: w.id, label: `${w.code} · ${w.name}` })),
  );

  protected open(row: StockLevel): void {
    this.router.navigate(['/inventory/products', row.productId]);
  }
}
