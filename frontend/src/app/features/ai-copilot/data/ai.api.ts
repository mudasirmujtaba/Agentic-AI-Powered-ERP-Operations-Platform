import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  AiAction,
  AiActionStatus,
  AiChatResponse,
  AiConversation,
  AiConversationSummary,
  DecideActionRequest,
} from './ai.models';

@Injectable({ providedIn: 'root' })
export class AiApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/ai`;

  chat(message: string, conversationId: string | null): Observable<AiChatResponse> {
    return this.http.post<AiChatResponse>(`${this.baseUrl}/chat`, { conversationId, message });
  }

  conversations(): Observable<AiConversationSummary[]> {
    return this.http.get<AiConversationSummary[]>(`${this.baseUrl}/conversations`);
  }

  conversation(id: string): Observable<AiConversation> {
    return this.http.get<AiConversation>(`${this.baseUrl}/conversations/${id}`);
  }

  actions(status?: AiActionStatus): Observable<AiAction[]> {
    return this.http.get<AiAction[]>(`${this.baseUrl}/actions`, { params: status ? { status } : {} });
  }

  approve(id: string, request: DecideActionRequest): Observable<AiAction> {
    return this.http.post<AiAction>(`${this.baseUrl}/actions/${id}/approve`, request);
  }

  reject(id: string, request: DecideActionRequest): Observable<AiAction> {
    return this.http.post<AiAction>(`${this.baseUrl}/actions/${id}/reject`, request);
  }
}
