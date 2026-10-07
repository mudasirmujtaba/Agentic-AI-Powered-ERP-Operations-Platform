import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { WriteAccess } from '../../../../core/auth/roles';
import { describeApiError } from '../../../../core/http/api-error';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { HasRoleDirective } from '../../../../shared/directives/has-role.directive';
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  TICKET_STATUSES,
  StatusStyle,
  TicketCategory,
  statusOptions,
} from '../../../../shared/models/statuses';
import { MarkdownLitePipe } from '../../../../shared/pipes/markdown-lite.pipe';
import { TicketsApi } from '../../data/tickets.api';
import { TicketInsights, TicketListItem, TicketStats } from '../../data/tickets.models';
import { Icon } from '../../../../shared/components/icon/icon';

@Component({
  selector: 'app-ticket-list',
  imports: [Icon, 
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSortModule,
    MatTableModule,
    PageHeader,
    StatusBadge,
    HasRoleDirective,
    MarkdownLitePipe,
  ],
  templateUrl: './ticket-list.html',
  styles: `
    :host ::ng-deep .answer p { margin: 0 0 0.5rem; }
    :host ::ng-deep .answer ul, :host ::ng-deep .answer ol { margin: 0 0 0.5rem; padding-left: 1.25rem; }
    :host ::ng-deep .answer ul { list-style: disc; }
    :host ::ng-deep .answer ol { list-style: decimal; }
  `,
})
export class TicketList {
  private readonly api = inject(TicketsApi);
  private readonly router = inject(Router);
  private readonly params = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly writeAccess = WriteAccess.tickets;
  // Indexed by untyped table cells, hence the widened key types.
  protected readonly statuses: Record<string, StatusStyle> = TICKET_STATUSES;
  protected readonly priorities: Record<string, StatusStyle> = TICKET_PRIORITIES;
  protected readonly categories: Record<string, string> = TICKET_CATEGORIES;
  protected readonly statusOptions = statusOptions(TICKET_STATUSES);
  protected readonly priorityOptions = statusOptions(TICKET_PRIORITIES);
  protected readonly categoryOptions = Object.entries(TICKET_CATEGORIES).map(([value, label]) => ({
    value: value as TicketCategory,
    label,
  }));
  protected readonly columns = ['ticketNumber', 'subject', 'customerName', 'category', 'priority', 'status', 'assignedToName', 'createdAtUtc'];

  // Filter state mirrored here so the stat tiles can set filters and the controls reflect them.
  protected readonly status = signal<string | undefined>(this.params.get('status') ?? undefined);
  protected readonly priority = signal<string | undefined>(this.params.get('priority') ?? undefined);
  protected readonly category = signal<string | undefined>(undefined);
  protected readonly openOnly = signal(this.params.get('openOnly') !== 'false');
  protected readonly mine = signal(this.params.get('mine') === 'true');

  protected readonly list = createPagedList((query) => this.api.list(query), {
    sortBy: 'createdAtUtc',
    sortDirection: 'desc',
    status: this.status(),
    priority: this.priority(),
    openOnly: this.openOnly() ? 'true' : undefined,
    assignedToMe: this.mine() ? 'true' : undefined,
  });

  protected readonly stats = toSignal<TicketStats | null>(this.api.stats().pipe(catchError(() => of(null))), {
    initialValue: null,
  });

  protected readonly insights = signal<TicketInsights | null>(null);
  protected readonly insightsLoading = signal(false);
  protected readonly insightsError = signal<string | null>(null);
  protected readonly insightDays = signal(90);

  protected setStatus(value: string | undefined): void {
    this.status.set(value);
    // A specific closed-state filter would otherwise be hidden by "open only".
    if (value === 'Resolved' || value === 'Closed') this.openOnly.set(false);
    this.applyFilters();
  }

  protected setPriority(value: string | undefined): void {
    this.priority.set(value);
    this.applyFilters();
  }

  protected setCategory(value: string | undefined): void {
    this.category.set(value);
    this.applyFilters();
  }

  protected setOpenOnly(value: boolean): void {
    this.openOnly.set(value);
    this.applyFilters();
  }

  protected setMine(value: boolean): void {
    this.mine.set(value);
    this.applyFilters();
  }

  /** Stat tiles are shortcuts into the matching filter. */
  protected showTile(tile: 'open' | 'urgent' | 'mine'): void {
    this.status.set(undefined);
    this.category.set(undefined);
    this.openOnly.set(true);
    this.priority.set(tile === 'urgent' ? 'Urgent' : undefined);
    this.mine.set(tile === 'mine');
    this.applyFilters();
  }

  protected findRecurringProblems(): void {
    this.insightsLoading.set(true);
    this.insightsError.set(null);
    this.api.insights(this.insightDays()).subscribe({
      next: (result) => {
        this.insights.set(result);
        this.insightsLoading.set(false);
      },
      error: (err) => {
        this.insightsError.set(describeApiError(err));
        this.insightsLoading.set(false);
      },
    });
  }

  protected open(ticket: TicketListItem): void {
    this.router.navigate(['/tickets', ticket.id]);
  }

  private applyFilters(): void {
    this.list.setFilter({
      status: this.status(),
      priority: this.priority(),
      category: this.category(),
      openOnly: this.openOnly() ? 'true' : undefined,
      assignedToMe: this.mine() ? 'true' : undefined,
    });
  }
}
