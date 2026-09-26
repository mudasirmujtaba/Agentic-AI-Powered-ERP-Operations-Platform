import { Routes } from '@angular/router';

import { FeaturePlaceholder } from '../../shared/components/feature-placeholder/feature-placeholder';

export const SALES_ROUTES: Routes = [
  { path: '', component: FeaturePlaceholder, data: { title: 'Sales' } },
];
