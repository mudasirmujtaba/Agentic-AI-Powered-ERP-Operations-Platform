import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ResourceApi } from '../../../shared/data/resource-api';
import { Category, Product, ProductListItem, SaveCategoryRequest, SaveProductRequest } from './catalog.models';

@Injectable({ providedIn: 'root' })
export class ProductsApi extends ResourceApi<ProductListItem, Product, SaveProductRequest> {
  protected readonly resource = 'products';
}

@Injectable({ providedIn: 'root' })
export class CategoriesApi extends ResourceApi<Category, Category, SaveCategoryRequest> {
  protected readonly resource = 'categories';

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
