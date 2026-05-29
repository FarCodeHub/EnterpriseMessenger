import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { BaseApiService } from '../api/base-api.service';
import { LoginRequest } from '../../features/auth/models/login-request';
import { RefreshTokenRequest } from '../../features/auth/models/refresh-token-request';
import { AuthResponse } from '../../features/auth/models/auth-response';
import { LogoutRequest } from '../../features/auth/models/logout-request';
import { LogoutResponse } from '../../features/auth/models/logout-response';
import { SessionDto } from '../../features/auth/models/session.dto';

@Injectable({
  providedIn: 'root'
})
export class AuthApiService extends BaseApiService {
  login(request: LoginRequest): Observable<AuthResponse> {
    return this.post<AuthResponse, LoginRequest>('/Auth/login', request);
  }

  refresh(request: RefreshTokenRequest): Observable<AuthResponse> {
    return this.post<AuthResponse, RefreshTokenRequest>('/Auth/refresh', request);
  }

  logout(request: LogoutRequest): Observable<LogoutResponse> {
    return this.post<LogoutResponse, LogoutRequest>('/Auth/logout', request);
  }

  logoutAll(): Observable<LogoutResponse> {
    return this.post<LogoutResponse, undefined>('/Auth/logout-all');
  }

  getSessions(): Observable<SessionDto[]> {
    return this.get<SessionDto[]>('/Auth/sessions');
  }
}