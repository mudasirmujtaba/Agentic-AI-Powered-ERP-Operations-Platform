export type SortDirection = 'asc' | 'desc';

export interface PagedQuery {
  page: number;
  pageSize: number;
  search?: string;
  sortBy?: string;
  sortDirection?: SortDirection;
  /** Resource-specific filters, e.g. `categoryId` for products. */
  [filter: string]: string | number | undefined;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
