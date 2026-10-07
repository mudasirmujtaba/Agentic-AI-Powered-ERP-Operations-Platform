import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';

import { WriteAccess } from '../../../../../core/auth/roles';
import { PageHeader } from '../../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../../shared/data/paged-list';
import { HasRoleDirective } from '../../../../../shared/directives/has-role.directive';
import { WarehousesApi } from '../../data/warehouses.api';
import { Warehouse } from '../../data/warehouses.models';
import { Icon } from '../../../../../shared/components/icon/icon';

@Component({
  selector: 'app-warehouse-list',
  imports: [Icon, 
    RouterLink,
    MatButtonModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
    HasRoleDirective,
  ],
  templateUrl: './warehouse-list.html',
})
export class WarehouseList {
  private readonly api = inject(WarehousesApi);
  private readonly router = inject(Router);

  protected readonly writeAccess = WriteAccess.warehouses;
  protected readonly columns = ['code', 'name', 'location', 'isActive'];
  protected readonly list = createPagedList((query) => this.api.list(query), { sortBy: 'code' });

  protected open(warehouse: Warehouse): void {
    this.router.navigate(['/inventory/warehouses', warehouse.id]);
  }
}
