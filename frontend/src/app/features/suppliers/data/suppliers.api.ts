import { Injectable } from '@angular/core';

import { ResourceApi } from '../../../shared/data/resource-api';
import { SaveSupplierRequest, Supplier, SupplierListItem } from './suppliers.models';

@Injectable({ providedIn: 'root' })
export class SuppliersApi extends ResourceApi<SupplierListItem, Supplier, SaveSupplierRequest> {
  protected readonly resource = 'suppliers';
}
