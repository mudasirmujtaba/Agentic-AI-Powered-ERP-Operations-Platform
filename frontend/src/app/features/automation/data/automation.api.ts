import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { InsightReport, JobStatus, NotificationFeed } from './automation.models';

@Injectable({ providedIn: 'root' })
export class AutomationApi {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiBaseUrl;

  jobs(): Observable<JobStatus[]> {
    return this.http.get<JobStatus[]>(`${this.api}/jobs`);
  }

  runJob(id: string): Observable<{ runId: string }> {
    return this.http.post<{ runId: string }>(`${this.api}/jobs/${id}/run`, {});
  }

  /** `null` (204) until the first scan has run. */
  latestInventoryRisk(): Observable<InsightReport | null> {
    return this.http.get<InsightReport | null>(`${this.api}/insights/inventory-risk/latest`);
  }

  notifications(take = 20): Observable<NotificationFeed> {
    return this.http.get<NotificationFeed>(`${this.api}/notifications`, { params: new HttpParams().set('take', take) });
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/notifications/${id}/read`, {});
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.api}/notifications/read-all`, {});
  }
}
