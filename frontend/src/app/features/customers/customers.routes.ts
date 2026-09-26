import { Routes } from '@angular/router';

import { FeaturePlaceholder } from '../../shared/components/feature-placeholder/feature-placeholder';

export const CUSTOMERS_ROUTES: Routes = [
  { path: '', component: FeaturePlaceholder, data: { title: 'Customers' } },
];
