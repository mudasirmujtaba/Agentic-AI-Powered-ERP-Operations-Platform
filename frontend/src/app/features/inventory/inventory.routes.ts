import { Routes } from '@angular/router';

export const INVENTORY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/stock-list/stock-list').then((m) => m.StockList),
  },
  {
    path: 'products/:productId',
    loadComponent: () => import('./pages/product-stock/product-stock').then((m) => m.ProductStockPage),
  },
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
