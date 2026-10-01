import { Routes } from '@angular/router';

export const SUPPLIERS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/supplier-list/supplier-list').then((m) => m.SupplierList),
  },
  {
    path: 'new',
    loadComponent: () => import('./pages/supplier-form/supplier-form').then((m) => m.SupplierForm),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/supplier-form/supplier-form').then((m) => m.SupplierForm),
  },
];
