import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { PagedQuery, PagedResult } from '../../../shared/models/paged';
import { AdjustStockRequest, ProductStock, StockLevel, TransferStockRequest } from './inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/inventory`;

  stock(query: PagedQuery): Observable<PagedResult<StockLevel>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
    }
    return this.http.get<PagedResult<StockLevel>>(`${this.baseUrl}/stock`, { params });
  }

  productStock(productId: string): Observable<ProductStock> {
    return this.http.get<ProductStock>(`${this.baseUrl}/stock/${productId}`);
  }

  adjust(request: AdjustStockRequest): Observable<ProductStock> {
    return this.http.post<ProductStock>(`${this.baseUrl}/adjustments`, request);
  }

  transfer(request: TransferStockRequest): Observable<ProductStock> {
    return this.http.post<ProductStock>(`${this.baseUrl}/transfers`, request);
  }
}
