import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Observable, catchError, of, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { CurrentUser, LoginRequest, LoginResponse } from './auth.models';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentUserSignal = signal<CurrentUser | null>(null);

  readonly currentUser = this.currentUserSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserSignal() !== null);

  constructor(
    private readonly http: HttpClient,
    private readonly tokenStorage: TokenStorageService,
  ) {}

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiBaseUrl}/auth/login`, request).pipe(
      tap((response) => {
        this.tokenStorage.setToken(response.accessToken);
        this.currentUserSignal.set(response.user);
      }),
    );
  }

  logout(): void {
    this.tokenStorage.clearToken();
    this.currentUserSignal.set(null);
  }

  /** Rehydrates the session from a stored token on app bootstrap. */
  restoreSession(): Observable<CurrentUser | null> {
    if (!this.tokenStorage.getToken()) {
      return of(null);
    }

    return this.http.get<CurrentUser>(`${environment.apiBaseUrl}/auth/me`).pipe(
      tap((user) => this.currentUserSignal.set(user)),
      catchError(() => {
        this.tokenStorage.clearToken();
        this.currentUserSignal.set(null);
        return of(null);
      }),
    );
  }
}
