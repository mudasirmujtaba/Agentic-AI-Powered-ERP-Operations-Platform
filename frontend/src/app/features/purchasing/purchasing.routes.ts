import { Routes } from '@angular/router';

export const PURCHASING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/purchase-order-list/purchase-order-list').then((m) => m.PurchaseOrderList),
  },
  {
    path: 'new',
    loadComponent: () => import('./pages/purchase-order-page/purchase-order-page').then((m) => m.PurchaseOrderPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/purchase-order-page/purchase-order-page').then((m) => m.PurchaseOrderPage),
  },
];
