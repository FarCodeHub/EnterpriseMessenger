import { Injectable } from '@angular/core';
import { AuthResponse } from '../../features/auth/models/auth-response';
import { AuthUserDto } from '../../features/auth/models/auth-user.dto';

const ACCESS_TOKEN_KEY = 'enterprise_messenger_access_token';
const REFRESH_TOKEN_KEY = 'enterprise_messenger_refresh_token';
const ID_TOKEN_KEY = 'enterprise_messenger_id_token';
const EXPIRES_AT_KEY = 'enterprise_messenger_access_token_expires_at';
const PROVIDER_KEY = 'enterprise_messenger_auth_provider';
const USER_KEY = 'enterprise_messenger_user';
const DEVICE_ID_KEY = 'enterprise_messenger_device_id';

/** Who issued the stored tokens: the organization's Keycloak, or the API's local development login. */
export type AuthProvider = 'keycloak' | 'local';

@Injectable({
  providedIn: 'root'
})
export class TokenStorageService {
  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  getIdToken(): string | null {
    return localStorage.getItem(ID_TOKEN_KEY);
  }

  getProvider(): AuthProvider {
    return localStorage.getItem(PROVIDER_KEY) === 'keycloak' ? 'keycloak' : 'local';
  }

  /** Epoch milliseconds when the access token expires, or null when unknown. */
  getExpiresAt(): number | null {
    const value = Number(localStorage.getItem(EXPIRES_AT_KEY));
    return Number.isFinite(value) && value > 0 ? value : null;
  }

  getUser(): AuthUserDto | null {
    const value = localStorage.getItem(USER_KEY);
    return value ? JSON.parse(value) as AuthUserDto : null;
  }

  getDeviceId(): string {
    let deviceId = localStorage.getItem(DEVICE_ID_KEY);

    if (!deviceId) {
      deviceId = crypto.randomUUID();
      localStorage.setItem(DEVICE_ID_KEY, deviceId);
    }

    return deviceId;
  }

  saveAuth(response: AuthResponse, provider: AuthProvider = 'local', idToken: string | null = null): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    localStorage.setItem(PROVIDER_KEY, provider);
    localStorage.setItem(EXPIRES_AT_KEY, String(Date.now() + response.expiresInSeconds * 1000));

    if (idToken) {
      localStorage.setItem(ID_TOKEN_KEY, idToken);
    }
  }

  clear(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(ID_TOKEN_KEY);
    localStorage.removeItem(EXPIRES_AT_KEY);
    localStorage.removeItem(PROVIDER_KEY);
    localStorage.removeItem(USER_KEY);
  }
}
