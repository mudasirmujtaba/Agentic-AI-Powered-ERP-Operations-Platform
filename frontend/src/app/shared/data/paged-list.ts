import { DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import {
  BehaviorSubject,
  Observable,
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  of,
  switchMap,
  tap,
} from 'rxjs';

import { describeApiError } from '../../core/http/api-error';
import { PagedQuery, PagedResult } from '../models/paged';

/**
 * Signal-based state for a server-paged, sortable, searchable table.
 * Must be created in an injection context (e.g. a component field initializer).
 */
export function createPagedList<T>(
  fetch: (query: PagedQuery) => Observable<PagedResult<T>>,
  initial: Partial<PagedQuery> = {},
) {
  const destroyRef = inject(DestroyRef);

  const items = signal<T[]>([]);
  const totalCount = signal(0);
  const loading = signal(true);
  const error = signal<string | null>(null);
  const query$ = new BehaviorSubject<PagedQuery>({ page: 1, pageSize: 20, ...initial });
  const query = signal(query$.value);
  const search$ = new Subject<string>();

  query$
    .pipe(
      tap((q) => {
        query.set(q);
        loading.set(true);
        error.set(null);
      }),
      switchMap((q) =>
        fetch(q).pipe(
          catchError((err: unknown) => {
            error.set(describeApiError(err));
            return of(null);
          }),
        ),
      ),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe((result) => {
      if (result) {
        items.set(result.items);
        totalCount.set(result.totalCount);
      }
      loading.set(false);
    });

  const update = (patch: Partial<PagedQuery>) => query$.next({ ...query$.value, ...patch });

  search$
    .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(destroyRef))
    .subscribe((search) => update({ search: search || undefined, page: 1 }));

  return {
    items: items.asReadonly(),
    totalCount: totalCount.asReadonly(),
    loading: loading.asReadonly(),
    error: error.asReadonly(),
    query: query.asReadonly(),
    onPage: (event: PageEvent) => update({ page: event.pageIndex + 1, pageSize: event.pageSize }),
    onSort: (sort: Sort) =>
      update({
        sortBy: sort.direction ? sort.active : undefined,
        sortDirection: sort.direction || undefined,
        page: 1,
      }),
    onSearch: (term: string) => search$.next(term.trim()),
    setFilter: (patch: Partial<PagedQuery>) => update({ ...patch, page: 1 }),
    reload: () => update({}),
  };
}

export type PagedList<T> = ReturnType<typeof createPagedList<T>>;
