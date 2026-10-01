import { Routes } from '@angular/router';

export const CUSTOMERS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/customer-list/customer-list').then((m) => m.CustomerList),
  },
  {
    path: 'new',
    loadComponent: () => import('./pages/customer-form/customer-form').then((m) => m.CustomerForm),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/customer-form/customer-form').then((m) => m.CustomerForm),
  },
];
