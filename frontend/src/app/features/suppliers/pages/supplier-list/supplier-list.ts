import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';

import { WriteAccess } from '../../../../core/auth/roles';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { HasRoleDirective } from '../../../../shared/directives/has-role.directive';
import { SuppliersApi } from '../../data/suppliers.api';
import { SupplierListItem } from '../../data/suppliers.models';
import { Icon } from '../../../../shared/components/icon/icon';

@Component({
  selector: 'app-supplier-list',
  imports: [Icon, 
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
    HasRoleDirective,
  ],
  templateUrl: './supplier-list.html',
})
export class SupplierList {
  private readonly api = inject(SuppliersApi);
  private readonly router = inject(Router);

  protected readonly writeAccess = WriteAccess.suppliers;
  protected readonly columns = ['code', 'name', 'contactName', 'email', 'averageLeadTimeDays', 'isActive'];
  protected readonly list = createPagedList((query) => this.api.list(query), { sortBy: 'name' });

  protected open(supplier: SupplierListItem): void {
    this.router.navigate(['/suppliers', supplier.id]);
  }
}
