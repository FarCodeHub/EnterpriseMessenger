import { Injectable } from '@angular/core';
import { AuthResponse } from '../../features/auth/models/auth-response';
import { AuthUserDto } from '../../features/auth/models/auth-user.dto';

const ACCESS_TOKEN_KEY = 'enterprise_messenger_access_token';
const REFRESH_TOKEN_KEY = 'enterprise_messenger_refresh_token';
const USER_KEY = 'enterprise_messenger_user';
const DEVICE_ID_KEY = 'enterprise_messenger_device_id';

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

  saveAuth(response: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
  }

  clear(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }
}