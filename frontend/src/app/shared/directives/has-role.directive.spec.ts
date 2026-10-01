import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AuthService } from '../../core/auth/auth.service';
import { HasRoleDirective } from './has-role.directive';

@Component({
  imports: [HasRoleDirective],
  template: `<button *appHasRole="['Administrator', 'SalesUser']" id="guarded">New</button>`,
})
class Host {}

describe('HasRoleDirective', () => {
  const roles = signal<string[]>([]);
  const authStub: Pick<AuthService, 'hasAnyRole'> = {
    hasAnyRole: (allowed: readonly string[]) => roles().some((r) => allowed.includes(r)),
  };

  beforeEach(() => {
    roles.set([]);
    TestBed.configureTestingModule({
      imports: [Host],
      providers: [{ provide: AuthService, useValue: authStub }],
    });
  });

  async function render() {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    return fixture;
  }

  it('hides content when the user has none of the roles', async () => {
    roles.set(['FinanceUser']);
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('#guarded')).toBeNull();
  });

  it('shows content when the user has one of the roles', async () => {
    roles.set(['SalesUser']);
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('#guarded')).not.toBeNull();
  });

  it('reacts when the user roles change', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('#guarded')).toBeNull();

    roles.set(['Administrator']);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#guarded')).not.toBeNull();

    roles.set([]);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#guarded')).toBeNull();
  });
});
