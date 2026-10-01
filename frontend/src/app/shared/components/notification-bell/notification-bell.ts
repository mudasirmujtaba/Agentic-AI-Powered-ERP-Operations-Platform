import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { catchError, of, switchMap, timer } from 'rxjs';

import { AutomationApi } from '../../../features/automation/data/automation.api';
import { AppNotification, NotificationFeed } from '../../../features/automation/data/automation.models';
import { MarkdownLitePipe } from '../../pipes/markdown-lite.pipe';

/** Polling interval for new notifications; jobs run at most a few times a day, so this can be lazy. */
const POLL_MS = 60_000;

@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe, MatBadgeModule, MatButtonModule, MatIconModule, MatMenuModule, MarkdownLitePipe],
  template: `
    <button
      mat-icon-button
      [matMenuTriggerFor]="menu"
      (menuOpened)="refresh()"
      [attr.aria-label]="feed().unreadCount ? feed().unreadCount + ' unread notifications' : 'Notifications'"
    >
      <mat-icon [matBadge]="feed().unreadCount || null" matBadgeColor="warn" matBadgeSize="small" aria-hidden="false">
        notifications
      </mat-icon>
    </button>

    <mat-menu #menu="matMenu" xPosition="before" class="notification-menu">
      <div class="flex items-center justify-between gap-4 px-4 py-2" (click)="$event.stopPropagation()" role="presentation">
        <span class="font-medium">Notifications</span>
        @if (feed().unreadCount) {
          <button mat-button (click)="markAllRead()">Mark all read</button>
        }
      </div>
      @for (n of feed().items; track n.id) {
        <button mat-menu-item class="notification-item" (click)="open(n)" [class.unread]="!n.readAtUtc">
          <div class="py-2">
            <div class="flex items-center gap-2">
              <mat-icon class="!mr-0 shrink-0" [class]="iconClass(n)">{{ icon(n) }}</mat-icon>
              <span class="font-medium" [class.opacity-70]="n.readAtUtc">{{ n.title }}</span>
            </div>
            <div class="notification-body text-xs opacity-80" [innerHTML]="n.body | markdownLite"></div>
            <div class="text-xs opacity-60">{{ n.createdAtUtc | date: 'short' }}</div>
          </div>
        </button>
      } @empty {
        <p class="m-0 px-4 py-3 text-sm opacity-70">No notifications yet.</p>
      }
    </mat-menu>
  `,
  styles: `
    :host ::ng-deep .notification-menu { max-width: 26rem !important; }
    :host ::ng-deep .notification-item { height: auto !important; line-height: 1.35 !important; white-space: normal !important; }
    :host ::ng-deep .notification-item.unread { background: color-mix(in srgb, var(--mat-sys-primary) 6%, transparent); }
    :host ::ng-deep .notification-body p, :host ::ng-deep .notification-body ul { margin: 0.25rem 0; padding-left: 1rem; }
    :host ::ng-deep .notification-body ul { list-style: disc; }
  `,
})
export class NotificationBell implements OnInit {
  private readonly api = inject(AutomationApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly feed = signal<NotificationFeed>({ items: [], unreadCount: 0 });

  ngOnInit(): void {
    timer(0, POLL_MS)
      .pipe(
        switchMap(() => this.api.notifications().pipe(catchError(() => of(null)))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((feed) => feed && this.feed.set(feed));
  }

  protected refresh(): void {
    this.api.notifications().pipe(catchError(() => of(null))).subscribe((feed) => feed && this.feed.set(feed));
  }

  protected icon(n: AppNotification): string {
    return n.severity === 'Critical' ? 'error' : n.severity === 'Warning' ? 'warning' : 'info';
  }

  protected iconClass(n: AppNotification): string {
    return n.severity === 'Critical' ? 'text-red-700' : n.severity === 'Warning' ? 'text-amber-700' : 'text-indigo-700';
  }

  protected open(n: AppNotification): void {
    if (!n.readAtUtc) {
      this.api.markRead(n.id).pipe(catchError(() => of(null))).subscribe(() => this.refresh());
    }
    if (n.link) this.router.navigateByUrl(n.link);
  }

  protected markAllRead(): void {
    this.api.markAllRead().pipe(catchError(() => of(null))).subscribe(() => this.refresh());
  }
}
