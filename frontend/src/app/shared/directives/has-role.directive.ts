import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

/** Renders its template only when the current user has at least one of the given roles: `*appHasRole="WriteAccess.customers"`. */
@Directive({ selector: '[appHasRole]' })
export class HasRoleDirective {
  readonly appHasRole = input.required<readonly string[]>();

  private readonly auth = inject(AuthService);
  private readonly templateRef = inject(TemplateRef);
  private readonly viewContainer = inject(ViewContainerRef);
  private rendered = false;

  constructor() {
    effect(() => {
      const allowed = this.auth.hasAnyRole(this.appHasRole());
      if (allowed && !this.rendered) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        this.viewContainer.clear();
        this.rendered = false;
      }
    });
  }
}
