import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError } from '../../../../core/http/api-error';
import { ConfirmDialog, ConfirmDialogData } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { createPagedList } from '../../../../shared/data/paged-list';
import { HasRoleDirective } from '../../../../shared/directives/has-role.directive';
import { CategoriesApi } from '../../data/catalog.api';
import { Category } from '../../data/catalog.models';

@Component({
  selector: 'app-category-list',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    PageHeader,
    HasRoleDirective,
  ],
  templateUrl: './category-list.html',
})
export class CategoryList {
  private readonly api = inject(CategoriesApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly writeAccess = WriteAccess.catalog;
  protected readonly list = createPagedList((query) => this.api.list(query), { sortBy: 'name' });

  protected readonly columns = computed(() => {
    const columns = ['name', 'description', 'productCount'];
    return this.auth.hasAnyRole(this.writeAccess) ? [...columns, 'actions'] : columns;
  });

  protected delete(category: Category, event: Event): void {
    event.stopPropagation();

    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
        data: {
          title: 'Delete category',
          message: `Delete "${category.name}"? This cannot be undone.`,
          confirmLabel: 'Delete',
        },
      })
      .afterClosed()
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.delete(category.id)),
      )
      .subscribe({
        next: () => {
          this.snackBar.open(`Category "${category.name}" deleted.`, 'Dismiss', { duration: 3000 });
          this.list.reload();
        },
        error: (err) => this.snackBar.open(describeApiError(err), 'Dismiss', { duration: 5000 }),
      });
  }
}
