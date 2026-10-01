import { Routes } from '@angular/router';

export const AI_COPILOT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/copilot/copilot').then((m) => m.Copilot),
  },
  {
    path: 'approvals',
    loadComponent: () => import('./pages/approvals/approvals').then((m) => m.Approvals),
  },
  {
    path: ':conversationId',
    loadComponent: () => import('./pages/copilot/copilot').then((m) => m.Copilot),
  },
];
