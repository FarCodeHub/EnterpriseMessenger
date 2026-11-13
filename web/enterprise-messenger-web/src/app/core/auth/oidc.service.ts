import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { KeycloakClientConfig } from './auth-config.service';

export interface OidcTokenResponse {
  access_token: string;
  refresh_token: string;
  expires_in: number;
  token_type: string;
  id_token?: string;
}

const PKCE_STATE_KEY = 'enterprise_messenger_oidc_state';
const PKCE_VERIFIER_KEY = 'enterprise_messenger_oidc_verifier';
const RETURN_URL_KEY = 'enterprise_messenger_oidc_return_url';

/**
 * Minimal OpenID Connect client for Keycloak: Authorization Code flow with PKCE (S256),
 * refresh-token grant and end-session redirect.
 */
@Injectable({
  providedIn: 'root'
})
export class OidcService {
  private readonly http = inject(HttpClient);

  get redirectUri(): string {
    return `${window.location.origin}/auth/callback`;
  }

  async beginLogin(config: KeycloakClientConfig, returnUrl = '/app/conversations'): Promise<void> {
    const state = this.randomString(32);
    const verifier = this.randomString(64);
    const challenge = await this.createChallenge(verifier);

    sessionStorage.setItem(PKCE_STATE_KEY, state);
    sessionStorage.setItem(PKCE_VERIFIER_KEY, verifier);
    sessionStorage.setItem(RETURN_URL_KEY, returnUrl);

    const params = new HttpParams()
      .set('client_id', config.clientId)
      .set('redirect_uri', this.redirectUri)
      .set('response_type', 'code')
      .set('scope', 'openid profile')
      .set('state', state)
      .set('code_challenge', challenge)
      .set('code_challenge_method', 'S256');

    window.location.assign(`${this.endpoint(config, 'auth')}?${params.toString()}`);
  }

  /** Validates the callback state and returns the PKCE verifier and where to go afterwards. */
  consumeCallbackState(state: string | null): { verifier: string; returnUrl: string } {
    const expectedState = sessionStorage.getItem(PKCE_STATE_KEY);
    const verifier = sessionStorage.getItem(PKCE_VERIFIER_KEY);
    const returnUrl = sessionStorage.getItem(RETURN_URL_KEY) || '/app/conversations';

    sessionStorage.removeItem(PKCE_STATE_KEY);
    sessionStorage.removeItem(PKCE_VERIFIER_KEY);
    sessionStorage.removeItem(RETURN_URL_KEY);

    if (!state || !expectedState || state !== expectedState || !verifier) {
      throw new Error('Invalid sign-in state.');
    }

    return { verifier, returnUrl };
  }

  exchangeCode(config: KeycloakClientConfig, code: string, verifier: string): Observable<OidcTokenResponse> {
    const body = new HttpParams()
      .set('grant_type', 'authorization_code')
      .set('client_id', config.clientId)
      .set('code', code)
      .set('redirect_uri', this.redirectUri)
      .set('code_verifier', verifier);

    return this.postForm(config, body);
  }

  refresh(config: KeycloakClientConfig, refreshToken: string): Observable<OidcTokenResponse> {
    const body = new HttpParams()
      .set('grant_type', 'refresh_token')
      .set('client_id', config.clientId)
      .set('refresh_token', refreshToken);

    return this.postForm(config, body);
  }

  logoutUrl(config: KeycloakClientConfig, idToken: string | null): string {
    let params = new HttpParams()
      .set('client_id', config.clientId)
      .set('post_logout_redirect_uri', `${window.location.origin}/auth/login`);

    if (idToken) {
      params = params.set('id_token_hint', idToken);
    }

    return `${this.endpoint(config, 'logout')}?${params.toString()}`;
  }

  isTokenEndpoint(url: string, config: KeycloakClientConfig | null): boolean {
    return !!config && url.startsWith(config.authority);
  }

  private postForm(config: KeycloakClientConfig, body: HttpParams): Observable<OidcTokenResponse> {
    return this.http.post<OidcTokenResponse>(this.endpoint(config, 'token'), body.toString(), {
      headers: new HttpHeaders({ 'Content-Type': 'application/x-www-form-urlencoded' })
    });
  }

  private endpoint(config: KeycloakClientConfig, name: 'auth' | 'token' | 'logout'): string {
    return `${config.authority}/protocol/openid-connect/${name}`;
  }

  private randomString(byteLength: number): string {
    const bytes = new Uint8Array(byteLength);
    crypto.getRandomValues(bytes);
    return this.base64Url(bytes);
  }

  private async createChallenge(verifier: string): Promise<string> {
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
    return this.base64Url(new Uint8Array(digest));
  }

  private base64Url(bytes: Uint8Array): string {
    let binary = '';
    bytes.forEach(byte => (binary += String.fromCharCode(byte)));

    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }
}
