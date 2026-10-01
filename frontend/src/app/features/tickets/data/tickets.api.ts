import { HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ResourceApi } from '../../../shared/data/resource-api';
import { TicketStatus } from '../../../shared/models/statuses';
import { SaveTicketRequest, Ticket, TicketAssignee, TicketInsights, TicketListItem, TicketStats } from './tickets.models';

@Injectable({ providedIn: 'root' })
export class TicketsApi extends ResourceApi<TicketListItem, Ticket, SaveTicketRequest> {
  protected readonly resource = 'tickets';

  stats(): Observable<TicketStats> {
    return this.http.get<TicketStats>(`${this.baseUrl}/stats`);
  }

  assignees(): Observable<TicketAssignee[]> {
    return this.http.get<TicketAssignee[]>(`${this.baseUrl}/assignees`);
  }

  changeStatus(id: string, status: TicketStatus, resolution: string | null = null): Observable<Ticket> {
    return this.action(id, 'status', { status, resolution });
  }

  addComment(id: string, body: string, isInternal: boolean): Observable<Ticket> {
    return this.action(id, 'comments', { body, isInternal });
  }

  summarize(id: string): Observable<Ticket> {
    return this.action(id, 'summary');
  }

  insights(days: number): Observable<TicketInsights> {
    return this.http.post<TicketInsights>(`${this.baseUrl}/insights`, {}, { params: new HttpParams().set('days', days) });
  }
}
