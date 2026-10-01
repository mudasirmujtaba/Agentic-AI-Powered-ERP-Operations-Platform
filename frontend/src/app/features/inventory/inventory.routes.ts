import { Routes } from '@angular/router';

import { FeaturePlaceholder } from '../../shared/components/feature-placeholder/feature-placeholder';

export const INVENTORY_ROUTES: Routes = [
  { path: '', component: FeaturePlaceholder, data: { title: 'Inventory' } },
  {
    path: 'warehouses',
    loadComponent: () => import('./warehouses/pages/warehouse-list/warehouse-list').then((m) => m.WarehouseList),
  },
  {
    path: 'warehouses/new',
    loadComponent: () => import('./warehouses/pages/warehouse-form/warehouse-form').then((m) => m.WarehouseForm),
  },
  {
    path: 'warehouses/:id',
    loadComponent: () => import('./warehouses/pages/warehouse-form/warehouse-form').then((m) => m.WarehouseForm),
  },
];
