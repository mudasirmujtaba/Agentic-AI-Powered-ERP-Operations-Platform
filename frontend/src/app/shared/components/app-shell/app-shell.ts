import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { WriteAccess } from '../../../core/auth/roles';
import { NotificationBell } from '../notification-bell/notification-bell';
import { Icon, IconName } from '../icon/icon';
import { Logo } from '../logo/logo';

interface NavLink {
  label: string;
  path: string;
  icon: IconName;
  /** Exact-match the route (for parents whose children have their own entries). */
  exact?: boolean;
  roles?: readonly string[];
}

interface NavGroup {
  label: string;
  links: NavLink[];
}

const ROLE_LABELS: Record<string, string> = {
  Administrator: 'Administrator',
  Manager: 'Manager',
  SalesUser: 'Sales',
  InventoryManager: 'Inventory',
  ProcurementUser: 'Procurement',
  FinanceUser: 'Finance',
};

const NAV: NavGroup[] = [
  {
    label: 'Overview',
    links: [
      { label: 'Dashboard', path: '/dashboard', icon: 'layout-dashboard' },
      { label: 'Reports', path: '/reports', icon: 'chart-column' },
    ],
  },
  {
    label: 'Sales & service',
    links: [
      { label: 'Customers', path: '/customers', icon: 'users' },
      { label: 'Sales orders', path: '/sales', icon: 'shopping-cart' },
      { label: 'Service tickets', path: '/tickets', icon: 'headset' },
    ],
  },
  {
    label: 'Supply chain',
    links: [
      { label: 'Products', path: '/products', icon: 'package', exact: true },
      { label: 'Categories', path: '/products/categories', icon: 'shapes' },
      { label: 'Inventory', path: '/inventory', icon: 'warehouse', exact: true },
      { label: 'Warehouses', path: '/inventory/warehouses', icon: 'map-pin' },
      { label: 'Suppliers', path: '/suppliers', icon: 'factory' },
      { label: 'Purchasing', path: '/purchasing', icon: 'truck' },
    ],
  },
  {
    label: 'Finance',
    links: [{ label: 'Invoices & payments', path: '/finance', icon: 'receipt-text' }],
  },
  {
    label: 'Operations assistant',
    links: [
      { label: 'Copilot', path: '/ai-copilot', icon: 'messages-square', exact: true },
      { label: 'Approvals', path: '/ai-copilot/approvals', icon: 'circle-check' },
      { label: 'Automation', path: '/automation', icon: 'clock' },
    ],
  },
  {
    label: 'Administration',
    links: [
      { label: 'Audit log', path: '/audit', icon: 'history', roles: WriteAccess.auditLog },
      { label: 'System health', path: '/system-health', icon: 'activity', roles: WriteAccess.jobs },
    ],
  },
];

@Component({
  selector: 'app-shell',
  imports: [Logo, Icon, RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatMenuModule, MatTooltipModule, NotificationBell],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css',
})
export class AppShell {
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly groups = computed(() =>
    NAV.map((group) => ({
      ...group,
      links: group.links.filter((link) => !link.roles || this.authService.hasAnyRole(link.roles)),
    })).filter((group) => group.links.length > 0),
  );

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  /** The section the current page belongs to: the longest matching nav link. */
  protected readonly section = computed(() => {
    const path = this.url().split('?')[0];
    const links = NAV.flatMap((g) => g.links.map((l) => ({ ...l, group: g.label })));
    return links
      .filter((l) => path === l.path || path.startsWith(l.path + '/'))
      .sort((a, b) => b.path.length - a.path.length)[0];
  });

  protected readonly initials = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return '';
    const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.trim();
    return (initials || user.email[0]).toUpperCase();
  });

  protected readonly displayName = computed(() => {
    const user = this.authService.currentUser();
    return user ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || user.email : '';
  });

  protected readonly roleLabel = computed(() =>
    (this.authService.currentUser()?.roles ?? []).map((r) => ROLE_LABELS[r] ?? r).join(', '),
  );

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
