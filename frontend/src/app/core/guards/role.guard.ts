import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../auth/auth.service';

/** Route guard factory: `canActivate: [roleGuard(['Administrator', 'Manager'])]`. UX only; the API enforces access. */
export function roleGuard(roles: readonly string[]): CanActivateFn {
  return () => (inject(AuthService).hasAnyRole(roles) ? true : inject(Router).createUrlTree(['/dashboard']));
}
