import { Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { BaseApiService } from '../api/base-api.service';

export interface KeycloakClientConfig {
  authority: string;
  clientId: string;
}

export interface AuthConfig {
  localLoginEnabled: boolean;
  keycloak: KeycloakClientConfig | null;
}

/**
 * Loads from the API how this deployment signs users in (Keycloak and/or the local development login),
 * so the same web build works in every environment.
 */
@Injectable({
  providedIn: 'root'
})
export class AuthConfigService extends BaseApiService {
  private readonly configSignal = signal<AuthConfig | null>(null);
  private loading: Promise<AuthConfig> | null = null;

  readonly config = this.configSignal.asReadonly();

  load(): Promise<AuthConfig> {
    const current = this.configSignal();

    if (current) {
      return Promise.resolve(current);
    }

    if (!this.loading) {
      this.loading = firstValueFrom(this.get<AuthConfig>('/Auth/config'))
        .then(config => {
          this.configSignal.set(config);
          return config;
        })
        .finally(() => {
          this.loading = null;
        });
    }

    return this.loading;
  }
}
