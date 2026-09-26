import { Routes } from '@angular/router';

import { FeaturePlaceholder } from '../../shared/components/feature-placeholder/feature-placeholder';

export const FINANCE_ROUTES: Routes = [
  { path: '', component: FeaturePlaceholder, data: { title: 'Finance' } },
];
