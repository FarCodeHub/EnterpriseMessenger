import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { AuthService } from '../../core/auth/auth.service';
import { LanguageSwitcherComponent } from '../../shared/components/language-switcher/language-switcher.component';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, TranslateModule, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen bg-slate-950 text-white">
      <aside class="flex w-80 flex-col border-e border-white/10 bg-slate-900">
        <div class="border-b border-white/10 p-4">
          <div class="flex items-center justify-between gap-3">
            <div>
              <h1 class="text-lg font-bold">
                {{ 'app.name' | translate }}
              </h1>
              <p class="text-xs text-slate-400">
                {{ authService.user()?.displayName }}
              </p>
            </div>

            <button
              type="button"
              class="rounded-lg bg-red-600 px-3 py-2 text-xs font-semibold hover:bg-red-500"
              (click)="logout()"
            >
              Logout
            </button>
          </div>

          <div class="mt-4">
            <app-language-switcher />
          </div>
        </div>

        <div class="flex-1 overflow-y-auto p-4">
          <div class="rounded-xl border border-dashed border-white/10 p-4 text-sm text-slate-400">
            Conversation list will be here
          </div>
        </div>
      </aside>

      <main class="flex min-w-0 flex-1 flex-col">
        <router-outlet />
      </main>
    </div>
  `
})
export class MainLayoutComponent {
  readonly authService = inject(AuthService);

  private readonly router = inject(Router);

  logout(): void {
    this.authService.logout().subscribe({
      next: () => {
        this.router.navigateByUrl('/auth/login');
      }
    });
  }
}