import { Routes } from '@angular/router';

export const TICKETS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/ticket-list/ticket-list').then((m) => m.TicketList),
  },
  {
    path: 'new',
    loadComponent: () => import('./pages/ticket-page/ticket-page').then((m) => m.TicketPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/ticket-page/ticket-page').then((m) => m.TicketPage),
  },
];
