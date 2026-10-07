import { HttpClient, HttpParams } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { environment } from '../../../../../environments/environment';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { createPagedList } from '../../../../shared/data/paged-list';
import { PagedQuery, PagedResult } from '../../../../shared/models/paged';
import { Icon } from '../../../../shared/components/icon/icon';

interface AuditEntry {
  id: string;
  occurredAtUtc: string;
  userEmail: string | null;
  action: string;
  entityType: string;
  entityId: string;
  summary: string;
  source: 'User' | 'AiAssisted' | 'System';
}

@Component({
  selector: 'app-audit-log',
  imports: [Icon, DatePipe, MatFormFieldModule, MatInputModule, MatPaginatorModule, MatProgressBarModule, MatSelectModule, MatTableModule, PageHeader, StatusBadge],
  templateUrl: './audit-log.html',
})
export class AuditLog {
  private readonly http = inject(HttpClient);

  protected readonly columns = ['occurredAtUtc', 'userEmail', 'source', 'action', 'entity', 'summary'];
  protected readonly list = createPagedList((query) => this.fetch(query), { pageSize: 50 });

  private fetch(query: PagedQuery) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== '') params = params.set(key, String(value));
    }
    return this.http.get<PagedResult<AuditEntry>>(`${environment.apiBaseUrl}/audit-logs`, { params });
  }
}
