import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, catchError, finalize, firstValueFrom, from, map, of, shareReplay, switchMap, tap, throwError } from 'rxjs';

import { AuthApiService } from './auth-api.service';
import { AuthConfigService, KeycloakClientConfig } from './auth-config.service';
import { OidcService, OidcTokenResponse } from './oidc.service';
import { TokenStorageService } from '../storage/token-storage.service';
import { applicationConfig } from '../config/application-config';
import { LoginRequest } from '../../features/auth/models/login-request';
import { AuthResponse } from '../../features/auth/models/auth-response';
import { AuthUserDto } from '../../features/auth/models/auth-user.dto';
import { deleteAnonymousDb, deleteLocalDbFor, deleteSharedLegacyDb, useLocalDbFor } from '../offline/app-local-db';

interface MeResponse {
  userId: string;
  userName: string;
  displayName: string | null;
}

// Refresh this long before the access token expires.
const REFRESH_MARGIN_MS = 60_000;

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly authApi = inject(AuthApiService);
  private readonly authConfig = inject(AuthConfigService);
  private readonly oidc = inject(OidcService);
  private readonly http = inject(HttpClient);
  private readonly tokenStorage = inject(TokenStorageService);

  private readonly userSignal = signal<AuthUserDto | null>(
    this.tokenStorage.getUser()
  );

  // The user whose data this tab has loaded into memory (states, signals); see storeSession.
  private userInThisTab: string | null = this.userSignal()?.id ?? null;

  private refreshInFlight: Observable<string> | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly user = this.userSignal.asReadonly();

  readonly isAuthenticated = computed(() => {
    return !!this.tokenStorage.getAccessToken() && !!this.userSignal();
  });

  constructor() {
    useLocalDbFor(this.userSignal()?.id ?? null);
    void deleteSharedLegacyDb().catch(() => undefined);
    this.scheduleRefresh();
  }

  /** Local username login (development only, unless the server explicitly allows it). */
  login(userName: string): Observable<AuthResponse> {
    const request: LoginRequest = {
      userName,
      deviceId: this.tokenStorage.getDeviceId()
    };

    return this.authApi.login(request).pipe(
      tap(response => this.storeSession(response, 'local'))
    );
  }

  /** Redirects the browser to the organization's sign-in page (Keycloak). */
  async loginWithOrganization(returnUrl?: string): Promise<void> {
    const config = await this.authConfig.load();

    if (!config.keycloak) {
      throw new Error('Organization sign-in is not configured.');
    }

    await this.oidc.beginLogin(config.keycloak, returnUrl);
  }

  /** Finishes the Keycloak redirect: exchanges the code, loads the messenger user and stores the session. */
  async completeOrganizationLogin(code: string, state: string | null): Promise<string> {
    const config = await this.authConfig.load();

    if (!config.keycloak) {
      throw new Error('Organization sign-in is not configured.');
    }

    const { verifier, returnUrl } = this.oidc.consumeCallbackState(state);
    const tokens = await firstValueFrom(this.oidc.exchangeCode(config.keycloak, code, verifier));
    const user = await firstValueFrom(this.loadUser(tokens.access_token));

    this.storeSession(this.toAuthResponse(tokens, user), 'keycloak', tokens.id_token ?? null);

    return returnUrl;
  }

  /**
   * Gets a new access token with the refresh token. Concurrent callers share one request.
   * Emits the new access token.
   */
  refresh(): Observable<string> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    const refreshToken = this.tokenStorage.getRefreshToken();

    if (!refreshToken) {
      return throwError(() => new Error('Refresh token is missing.'));
    }

    const request$ = this.tokenStorage.getProvider() === 'keycloak'
      ? this.refreshWithKeycloak(refreshToken)
      : this.refreshLocal(refreshToken);

    this.refreshInFlight = request$.pipe(
      map(() => this.tokenStorage.getAccessToken() ?? ''),
      finalize(() => {
        this.refreshInFlight = null;
      }),
      shareReplay(1)
    );

    return this.refreshInFlight;
  }

  /**
   * Signs out and deletes this user's local data (cached messages, unsent changes): the next person on this browser
   * must not see them. Reload the page afterwards so nothing stays in memory either.
   */
  logout(): Observable<void> {
    if (this.tokenStorage.getProvider() === 'keycloak') {
      return this.logoutFromOrganization();
    }

    const refreshToken = this.tokenStorage.getRefreshToken();
    const deviceId = this.tokenStorage.getDeviceId();
    const userId = this.userSignal()?.id ?? null;

    if (!refreshToken) {
      this.clearLocalAuth();
      return from(this.forgetLocalData(userId));
    }

    return this.authApi.logout({
      refreshToken,
      deviceId
    }).pipe(
      catchError(() => of(null)),
      finalize(() => {
        this.clearLocalAuth();
      }),
      switchMap(() => from(this.forgetLocalData(userId)))
    );
  }

  logoutAll(): Observable<void> {
    if (this.tokenStorage.getProvider() === 'keycloak') {
      return this.logoutFromOrganization();
    }

    const userId = this.userSignal()?.id ?? null;

    return this.authApi.logoutAll().pipe(
      catchError(() => of(null)),
      finalize(() => {
        this.clearLocalAuth();
      }),
      switchMap(() => from(this.forgetLocalData(userId)))
    );
  }

  getAccessToken(): string | null {
    return this.tokenStorage.getAccessToken();
  }

  /**
   * Clears the session without contacting the server, e.g. after the refresh token is rejected. The user's local
   * data stays (their database), so unsent messages survive an expired session; another user gets their own.
   */
  clearLocalAuth(): void {
    this.cancelRefreshTimer();
    this.tokenStorage.clear();
    this.userSignal.set(null);
    useLocalDbFor(null);
  }

  private refreshLocal(refreshToken: string): Observable<void> {
    return this.authApi.refresh({
      refreshToken,
      deviceId: this.tokenStorage.getDeviceId()
    }).pipe(
      tap(response => this.storeSession(response, 'local')),
      map(() => void 0)
    );
  }

  private refreshWithKeycloak(refreshToken: string): Observable<void> {
    return from(this.requireKeycloakConfig()).pipe(
      switchMap(config => this.oidc.refresh(config, refreshToken)),
      tap(tokens => {
        const user = this.userSignal();

        if (!user) {
          throw new Error('User is missing.');
        }

        this.storeSession(this.toAuthResponse(tokens, user), 'keycloak', tokens.id_token ?? null);
      }),
      map(() => void 0)
    );
  }

  private logoutFromOrganization(): Observable<void> {
    const idToken = this.tokenStorage.getIdToken();
    const userId = this.userSignal()?.id ?? null;

    return from(this.requireKeycloakConfig()).pipe(
      map(config => this.oidc.logoutUrl(config, idToken)),
      catchError(() => of(null)),
      // Before redirecting away: the page does not come back to finish it.
      switchMap(logoutUrl => from(this.forgetLocalData(userId)).pipe(map(() => logoutUrl))),
      tap(logoutUrl => {
        this.clearLocalAuth();

        // Ends the Keycloak session too, so the next sign-in asks for credentials again.
        if (logoutUrl) {
          window.location.assign(logoutUrl);
        }
      }),
      map(() => void 0)
    );
  }

  private async requireKeycloakConfig(): Promise<KeycloakClientConfig> {
    const config = await this.authConfig.load();

    if (!config.keycloak) {
      throw new Error('Organization sign-in is not configured.');
    }

    return config.keycloak;
  }

  private loadUser(accessToken: string): Observable<AuthUserDto> {
    return this.http.get<MeResponse>(`${applicationConfig.apiBaseUrl}/Auth/me`, {
      headers: new HttpHeaders({ Authorization: `Bearer ${accessToken}` })
    }).pipe(
      map(me => ({
        id: me.userId,
        userName: me.userName,
        displayName: me.displayName ?? me.userName
      }))
    );
  }

  private toAuthResponse(tokens: OidcTokenResponse, user: AuthUserDto): AuthResponse {
    return {
      accessToken: tokens.access_token,
      refreshToken: tokens.refresh_token,
      tokenType: tokens.token_type,
      expiresInSeconds: tokens.expires_in,
      user
    };
  }

  private storeSession(response: AuthResponse, provider: 'local' | 'keycloak', idToken: string | null = null): void {
    this.tokenStorage.saveAuth(response, provider, idToken);
    this.userSignal.set(response.user);
    useLocalDbFor(response.user.id);
    void deleteAnonymousDb().catch(() => undefined);
    this.scheduleRefresh();

    // Another user signed in in a tab that has the previous user's conversations in memory (their session expired
    // and someone else signed in): start the app fresh with the new user's data.
    const previousUser = this.userInThisTab;
    this.userInThisTab = response.user.id;

    if (previousUser && previousUser !== response.user.id) {
      window.location.reload();
    }
  }

  private async forgetLocalData(userId: string | null): Promise<void> {
    if (!userId) {
      return;
    }

    try {
      await deleteLocalDbFor(userId);
    } catch {
      // Blocked by another open tab of this user; the data stays only in that user's own database.
    }
  }

  /** Refreshes the access token shortly before it expires so requests and SignalR reconnects never use a stale one. */
  private scheduleRefresh(): void {
    this.cancelRefreshTimer();

    const expiresAt = this.tokenStorage.getExpiresAt();

    if (!expiresAt || !this.tokenStorage.getRefreshToken()) {
      return;
    }

    const delay = Math.max(expiresAt - Date.now() - REFRESH_MARGIN_MS, 0);

    this.refreshTimer = setTimeout(() => {
      this.refresh().subscribe({
        error: () => {
          // The interceptor handles a rejected refresh on the next API call.
        }
      });
    }, delay);
  }

  private cancelRefreshTimer(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
  }
}
