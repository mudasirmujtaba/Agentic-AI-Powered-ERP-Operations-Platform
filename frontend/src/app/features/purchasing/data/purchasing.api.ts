import { Injectable } from '@angular/core';

import { ResourceApi } from '../../../shared/data/resource-api';
import { PurchaseOrder, PurchaseOrderListItem, SavePurchaseOrderRequest } from './purchasing.models';

@Injectable({ providedIn: 'root' })
export class PurchaseOrdersApi extends ResourceApi<PurchaseOrderListItem, PurchaseOrder, SavePurchaseOrderRequest> {
  protected readonly resource = 'purchase-orders';
}
