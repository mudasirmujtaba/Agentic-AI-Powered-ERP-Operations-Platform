import { Injectable } from '@angular/core';

import { ResourceApi } from '../../../shared/data/resource-api';
import { Customer, CustomerListItem, SaveCustomerRequest } from './customers.models';

@Injectable({ providedIn: 'root' })
export class CustomersApi extends ResourceApi<CustomerListItem, Customer, SaveCustomerRequest> {
  protected readonly resource = 'customers';
}
