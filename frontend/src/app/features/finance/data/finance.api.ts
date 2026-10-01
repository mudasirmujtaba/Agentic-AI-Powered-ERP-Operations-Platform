import { Injectable } from '@angular/core';

import { ResourceApi } from '../../../shared/data/resource-api';
import { CreateInvoiceRequest, Invoice, InvoiceListItem } from './finance.models';

@Injectable({ providedIn: 'root' })
export class InvoicesApi extends ResourceApi<InvoiceListItem, Invoice, CreateInvoiceRequest> {
  protected readonly resource = 'invoices';
}
