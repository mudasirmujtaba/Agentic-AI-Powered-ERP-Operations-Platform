import { Routes } from '@angular/router';

import { WriteAccess } from './core/auth/roles';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: '',
    loadComponent: () => import('./shared/components/app-shell/app-shell').then((m) => m.AppShell),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'customers',
        loadChildren: () => import('./features/customers/customers.routes').then((m) => m.CUSTOMERS_ROUTES),
      },
      {
        path: 'suppliers',
        loadChildren: () => import('./features/suppliers/suppliers.routes').then((m) => m.SUPPLIERS_ROUTES),
      },
      {
        path: 'products',
        loadChildren: () => import('./features/products/products.routes').then((m) => m.PRODUCTS_ROUTES),
      },
      {
        path: 'inventory',
        loadChildren: () => import('./features/inventory/inventory.routes').then((m) => m.INVENTORY_ROUTES),
      },
      {
        path: 'sales',
        loadChildren: () => import('./features/sales/sales.routes').then((m) => m.SALES_ROUTES),
      },
      {
        path: 'purchasing',
        loadChildren: () => import('./features/purchasing/purchasing.routes').then((m) => m.PURCHASING_ROUTES),
      },
      {
        path: 'finance',
        loadChildren: () => import('./features/finance/finance.routes').then((m) => m.FINANCE_ROUTES),
      },
      {
        path: 'tickets',
        loadChildren: () => import('./features/tickets/tickets.routes').then((m) => m.TICKETS_ROUTES),
      },
      {
        path: 'audit',
        canActivate: [roleGuard(WriteAccess.auditLog)],
        loadComponent: () => import('./features/audit/pages/audit-log/audit-log').then((m) => m.AuditLog),
      },
      {
        path: 'ai-copilot',
        loadChildren: () => import('./features/ai-copilot/ai-copilot.routes').then((m) => m.AI_COPILOT_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
