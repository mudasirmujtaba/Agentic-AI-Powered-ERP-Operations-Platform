import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';

interface NavLink {
  label: string;
  path: string;
}

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
  readonly navLinks: NavLink[] = [
    { label: 'Dashboard', path: '/dashboard' },
    { label: 'Customers', path: '/customers' },
    { label: 'Suppliers', path: '/suppliers' },
    { label: 'Products', path: '/products' },
    { label: 'Inventory', path: '/inventory' },
    { label: 'Sales', path: '/sales' },
    { label: 'Purchasing', path: '/purchasing' },
    { label: 'Finance', path: '/finance' },
    { label: 'Service Tickets', path: '/tickets' },
    { label: 'AI Copilot', path: '/ai-copilot' },
  ];

  constructor(
    readonly authService: AuthService,
    private readonly router: Router,
  ) {}

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
