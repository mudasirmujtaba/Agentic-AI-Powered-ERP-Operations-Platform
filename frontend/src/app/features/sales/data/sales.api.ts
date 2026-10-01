import { Injectable } from '@angular/core';

import { ResourceApi } from '../../../shared/data/resource-api';
import { SalesOrder, SalesOrderListItem, SaveSalesOrderRequest } from './sales.models';

@Injectable({ providedIn: 'root' })
export class SalesOrdersApi extends ResourceApi<SalesOrderListItem, SalesOrder, SaveSalesOrderRequest> {
  protected readonly resource = 'sales-orders';
}
