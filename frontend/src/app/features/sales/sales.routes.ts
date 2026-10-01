import { Routes } from '@angular/router';

export const SALES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/sales-order-list/sales-order-list').then((m) => m.SalesOrderList),
  },
  {
    path: 'new',
    loadComponent: () => import('./pages/sales-order-page/sales-order-page').then((m) => m.SalesOrderPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/sales-order-page/sales-order-page').then((m) => m.SalesOrderPage),
  },
];
