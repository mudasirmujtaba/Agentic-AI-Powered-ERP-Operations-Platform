export interface LoginRequest {
  email: string;
  password: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: CurrentUser;
}
