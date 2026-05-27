import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { AuthApiService } from './auth-api.service';
import { TokenStorageService } from '../storage/token-storage.service';
import { LoginRequest } from '../../features/auth/models/login-request';
import { AuthResponse } from '../../features/auth/models/auth-response';
import { AuthUserDto } from '../../features/auth/models/auth-user.dto';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly authApi = inject(AuthApiService);
  private readonly tokenStorage = inject(TokenStorageService);

  private readonly userSignal = signal<AuthUserDto | null>(
    this.tokenStorage.getUser()
  );

  readonly user = this.userSignal.asReadonly();

  readonly isAuthenticated = computed(() => {
    return !!this.tokenStorage.getAccessToken() && !!this.userSignal();
  });

  login(userName: string): Observable<AuthResponse> {
    const request: LoginRequest = {
      userName,
      deviceId: this.tokenStorage.getDeviceId()
    };

    return this.authApi.login(request).pipe(
      tap(response => {
        this.tokenStorage.saveAuth(response);
        this.userSignal.set(response.user);
      })
    );
  }

  refresh(): Observable<AuthResponse> {
    const refreshToken = this.tokenStorage.getRefreshToken();

    if (!refreshToken) {
      throw new Error('Refresh token is missing.');
    }

    return this.authApi.refresh({
      refreshToken,
      deviceId: this.tokenStorage.getDeviceId()
    }).pipe(
      tap(response => {
        this.tokenStorage.saveAuth(response);
        this.userSignal.set(response.user);
      })
    );
  }

  logout(): void {
    this.tokenStorage.clear();
    this.userSignal.set(null);
  }

  getAccessToken(): string | null {
    return this.tokenStorage.getAccessToken();
  }
}