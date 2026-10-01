import { HttpClient, HttpParams } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { PagedQuery, PagedResult } from '../models/paged';

/** Typed client for a standard list/get/create/update REST resource under `/api/{resource}`. */
export abstract class ResourceApi<TListItem, TDetail, TSave> {
  protected readonly http = inject(HttpClient);
  protected abstract readonly resource: string;

  protected get baseUrl(): string {
    return `${environment.apiBaseUrl}/${this.resource}`;
  }

  list(query: PagedQuery): Observable<PagedResult<TListItem>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<TListItem>>(this.baseUrl, { params });
  }

  get(id: string): Observable<TDetail> {
    return this.http.get<TDetail>(`${this.baseUrl}/${id}`);
  }

  create(request: TSave): Observable<TDetail> {
    return this.http.post<TDetail>(this.baseUrl, request);
  }

  update(id: string, request: TSave): Observable<TDetail> {
    return this.http.put<TDetail>(`${this.baseUrl}/${id}`, request);
  }
}
