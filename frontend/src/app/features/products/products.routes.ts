import { Routes } from '@angular/router';

export const PRODUCTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/product-list/product-list').then((m) => m.ProductList),
  },
  {
    path: 'new',
    loadComponent: () => import('./pages/product-form/product-form').then((m) => m.ProductForm),
  },
  {
    path: 'categories',
    loadComponent: () => import('./pages/category-list/category-list').then((m) => m.CategoryList),
  },
  {
    path: 'categories/new',
    loadComponent: () => import('./pages/category-form/category-form').then((m) => m.CategoryForm),
  },
  {
    path: 'categories/:id',
    loadComponent: () => import('./pages/category-form/category-form').then((m) => m.CategoryForm),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/product-form/product-form').then((m) => m.ProductForm),
  },
];
