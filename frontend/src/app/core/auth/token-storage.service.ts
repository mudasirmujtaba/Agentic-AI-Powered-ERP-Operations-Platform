import { Injectable } from '@angular/core';

const TOKEN_KEY = 'opspilot.accessToken';

@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  getToken(): string | null {
    try {
      return localStorage.getItem(TOKEN_KEY);
    } catch {
      return null;
    }
  }

  setToken(token: string): void {
    try {
      localStorage.setItem(TOKEN_KEY, token);
    } catch {
      // Storage unavailable (private browsing, blocked site data, etc.) — auth
      // simply won't persist across reloads, which is an acceptable fallback.
    }
  }

  clearToken(): void {
    try {
      localStorage.removeItem(TOKEN_KEY);
    } catch {
      // No-op — nothing to clear if storage is unavailable.
    }
  }
}
