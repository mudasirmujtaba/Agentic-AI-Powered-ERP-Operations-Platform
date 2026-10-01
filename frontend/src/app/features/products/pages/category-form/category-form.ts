import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth/auth.service';
import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError, handleFormError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { firstError } from '../../../../shared/forms/form-errors';
import { CategoriesApi } from '../../data/catalog.api';

@Component({
  selector: 'app-category-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    PageHeader,
  ],
  templateUrl: './category-form.html',
})
export class CategoryForm implements OnInit {
  readonly id = input<string>();

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CategoriesApi);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly firstError = firstError;
  protected readonly isNew = computed(() => !this.id());
  protected readonly canEdit = computed(() => this.auth.hasAnyRole(WriteAccess.catalog));
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly title = signal('New category');

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
  });

  ngOnInit(): void {
    if (!this.canEdit()) {
      this.form.disable();
    }

    const id = this.id();
    if (!id) {
      return;
    }

    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (category) => {
        this.title.set(category.name);
        this.form.patchValue({ name: category.name, description: category.description ?? '' });
        this.loading.set(false);
      },
      error: (err) => {
        this.formError.set(describeApiError(err));
        this.loading.set(false);
      },
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    const value = this.form.getRawValue();
    const request = { name: value.name.trim(), description: value.description.trim() || null };

    const id = this.id();
    (id ? this.api.update(id, request) : this.api.create(request)).subscribe({
      next: (category) => {
        this.saving.set(false);
        this.snackBar.open(`Category "${category.name}" saved.`, 'Dismiss', { duration: 3000 });
        this.router.navigate(['/products/categories']);
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(handleFormError(this.form, err, 'name'));
      },
    });
  }
}
