import { Injectable } from '@angular/core';

import { ResourceApi } from '../../../../shared/data/resource-api';
import { SaveWarehouseRequest, Warehouse } from './warehouses.models';

@Injectable({ providedIn: 'root' })
export class WarehousesApi extends ResourceApi<Warehouse, Warehouse, SaveWarehouseRequest> {
  protected readonly resource = 'warehouses';
}
