import { Routes } from '@angular/router';

export const FINANCE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/invoice-list/invoice-list').then((m) => m.InvoiceList),
  },
  {
    path: 'invoices/:id',
    loadComponent: () => import('./pages/invoice-page/invoice-page').then((m) => m.InvoicePage),
  },
];
