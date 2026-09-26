import { Routes } from '@angular/router';

import { FeaturePlaceholder } from '../../shared/components/feature-placeholder/feature-placeholder';

export const AI_COPILOT_ROUTES: Routes = [
  { path: '', component: FeaturePlaceholder, data: { title: 'AI Copilot' } },
];
