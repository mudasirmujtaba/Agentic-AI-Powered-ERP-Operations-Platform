import { Component, computed } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { WriteAccess } from '../../../core/auth/roles';

interface NavLink {
  label: string;
  path: string;
  /** Sub-page of the link above it; rendered indented. */
  child?: boolean;
  /** Only shown to these roles. */
  roles?: readonly string[];
}

const NAV_LINKS: NavLink[] = [
  { label: 'Dashboard', path: '/dashboard' },
  { label: 'AI Copilot', path: '/ai-copilot' },
  { label: 'Approvals', path: '/ai-copilot/approvals', child: true },
  { label: 'Customers', path: '/customers' },
  { label: 'Suppliers', path: '/suppliers' },
  { label: 'Products', path: '/products' },
  { label: 'Categories', path: '/products/categories', child: true },
  { label: 'Inventory', path: '/inventory' },
  { label: 'Warehouses', path: '/inventory/warehouses', child: true },
  { label: 'Sales', path: '/sales' },
  { label: 'Purchasing', path: '/purchasing' },
  { label: 'Finance', path: '/finance' },
  { label: 'Service Tickets', path: '/tickets' },
  { label: 'Audit log', path: '/audit', roles: WriteAccess.auditLog },
];

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css',
})
export class AppShell {
  readonly navLinks = computed(() => NAV_LINKS.filter((link) => !link.roles || this.authService.hasAnyRole(link.roles)));

  constructor(
    readonly authService: AuthService,
    private readonly router: Router,
  ) {}

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
