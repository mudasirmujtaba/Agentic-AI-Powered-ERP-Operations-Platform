import { CurrencyPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { WriteAccess } from '../../../../core/auth/roles';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { HasRoleDirective } from '../../../../shared/directives/has-role.directive';
import { CategoriesApi, ProductsApi } from '../../data/catalog.api';
import { ProductListItem } from '../../data/catalog.models';

@Component({
  selector: 'app-product-list',
  imports: [
    CurrencyPipe,
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
  templateUrl: './product-list.html',
})
export class ProductList {
  private readonly api = inject(ProductsApi);
  private readonly categoriesApi = inject(CategoriesApi);
  private readonly router = inject(Router);

  protected readonly writeAccess = WriteAccess.catalog;
  protected readonly columns = ['code', 'name', 'categoryName', 'unitPrice', 'reorderPoint', 'safetyStock', 'primarySupplierName', 'isActive'];
  protected readonly list = createPagedList((query) => this.api.list(query), { sortBy: 'code' });

  protected readonly categories = toSignal(
    this.categoriesApi.list({ page: 1, pageSize: 100, sortBy: 'name' }).pipe(
      map((result) => result.items),
      catchError(() => of([])),
    ),
    { initialValue: [] },
  );

  protected filterByCategory(categoryId: string | undefined): void {
    this.list.setFilter({ categoryId });
  }

  protected open(product: ProductListItem): void {
    this.router.navigate(['/products', product.id]);
  }
}
