import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { AuthService } from '../../../core/auth/auth.service';

/** Landing page for the Keycloak redirect after sign-in. */
@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [TranslateModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 flex items-center justify-center px-4">
      <div class="w-full max-w-md rounded-2xl bg-white/10 border border-white/10 shadow-2xl p-8 text-center">
        @if (errorMessage()) {
          <div class="rounded-xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-300">
            {{ errorMessage()! | translate }}
          </div>

          <a routerLink="/auth/login" class="mt-6 inline-block text-sm text-blue-400 hover:text-blue-300">
            {{ 'auth.callback.backToLogin' | translate }}
          </a>
        } @else {
          <p class="text-slate-200">{{ 'auth.callback.signingIn' | translate }}</p>
        }
      </div>
    </div>
  `
})
export class AuthCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const params = this.route.snapshot.queryParamMap;
    const code = params.get('code');

    if (params.get('error') || !code) {
      this.errorMessage.set('auth.callback.failed');
      return;
    }

    try {
      const returnUrl = await this.authService.completeOrganizationLogin(code, params.get('state'));
      await this.router.navigateByUrl(returnUrl, { replaceUrl: true });
    } catch {
      this.errorMessage.set('auth.callback.failed');
    }
  }
}
