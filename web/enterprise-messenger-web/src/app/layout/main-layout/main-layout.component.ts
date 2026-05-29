import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { AuthService } from '../../core/auth/auth.service';
import { ChatSignalRService } from '../../core/signalr/chat-signalr.service';
import { LanguageSwitcherComponent } from '../../shared/components/language-switcher/language-switcher.component';
import { ConversationListComponent } from '../../features/conversations/conversation-list/conversation-list.component';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    RouterOutlet,
    TranslateModule,
    LanguageSwitcherComponent,
    ConversationListComponent
  ],
  template: `
    <div class="grid h-dvh grid-cols-[1fr_320px] overflow-hidden bg-slate-950 text-white">
      <main class="min-h-0 min-w-0 overflow-hidden">
        <router-outlet />
      </main>

      <aside class="min-h-0 border-s border-white/10 bg-slate-900">
        <div class="flex h-full min-h-0 flex-col">
          <div class="shrink-0 border-b border-white/10 p-4">
            <div class="flex items-center justify-between gap-3">
              <button
                type="button"
                class="rounded-lg bg-red-600 px-3 py-2 text-xs font-semibold hover:bg-red-500"
                (click)="logout()"
              >
                {{ 'layout.logout' | translate }}
              </button>

              <div class="text-end">
                <h1 class="text-lg font-bold">
                  {{ 'app.name' | translate }}
                </h1>
                <p class="text-xs text-slate-400">
                  {{ authService.user()?.displayName }}
                </p>
              </div>
            </div>

            <div class="mt-4">
              <app-language-switcher />
            </div>
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto p-4">
            <app-conversation-list />
          </div>
        </div>
      </aside>
    </div>
  `
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  readonly authService = inject(AuthService);

  private readonly router = inject(Router);
  private readonly chatSignalR = inject(ChatSignalRService);

  ngOnInit(): void {
    this.chatSignalR.connect();
  }

  ngOnDestroy(): void {
    this.chatSignalR.disconnect();
  }

  logout(): void {
    this.chatSignalR.disconnect().finally(() => {
      this.authService.logout().subscribe({
        next: () => {
          this.router.navigateByUrl('/auth/login');
        }
      });
    });
  }
}