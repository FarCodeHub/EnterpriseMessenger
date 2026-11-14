import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ChatSignalRService } from '../../core/signalr/chat-signalr.service';
import { LanguageSwitcherComponent } from '../../shared/components/language-switcher/language-switcher.component';
import { ConversationListComponent } from '../../features/conversations/conversation-list/conversation-list.component';
import { PresenceState } from '../../state/presence/presence.state';
import { ConversationsState } from '../../state/conversations/conversations.state';
import { ConnectionStateService } from '../../core/offline/connection-state.service';
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
<div class="relative h-dvh overflow-hidden bg-slate-950 text-white">  
     @if (connectionState.state() !== 'connected') {
    <div
      class="pointer-events-none absolute left-1/2 top-3 z-50 -translate-x-1/2 rounded-full
             bg-slate-800/95 px-4 py-2 text-xs font-medium shadow-lg ring-1 ring-white/10"
    >
      @if (connectionState.state() === 'connecting') {
        <span class="text-amber-200">🔄{{'common.Connecting' |translate}} </span>
      } @else {
        <span class="text-red-200">🔴 {{'presence.offline' |translate}}</span>
      }
    </div>
  }

    <div dir="ltr" class="grid h-full grid-cols-[1fr_320px] overflow-hidden">
    <main class="min-h-0 min-w-0 overflow-hidden">
      <router-outlet />
    </main>

  <aside class="min-h-0 border-s border-white/10 bg-slate-900">     
  

<div class="flex h-full min-h-0 flex-col">
          <div class="shrink-0 border-b border-white/10 p-4">
<div class="flex items-center justify-between gap-3">
  <div class="text-start">
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
    {{ 'layout.logout' | translate }}
  </button>
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
    </div>
  `
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  readonly authService = inject(AuthService);
  private readonly destroy$ = new Subject<void>();
  private readonly conversationsState = inject(ConversationsState);
  private readonly router = inject(Router);
  private readonly chatSignalR = inject(ChatSignalRService);
  readonly connectionState = inject(ConnectionStateService);
  private readonly presenceState = inject(PresenceState);
  ngOnInit(): void {
    this.chatSignalR.connect();

    this.chatSignalR.conversationUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.conversationsState.handleConversationUpdated(event);
      });


  //     this.chatSignalR.presenceOnline$
  // .pipe(takeUntil(this.destroy$))
  // .subscribe(userId => {
  //   this.presenceState.setOnline(userId);
  // });

// this.chatSignalR.presenceOffline$
//   .pipe(takeUntil(this.destroy$))
//   .subscribe(userId => {
//     this.presenceState.setOffline(userId);
//   });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();

    this.chatSignalR.disconnect();
  }

  logout(): void {
    this.chatSignalR.disconnect().finally(() => {
      this.authService.logout().subscribe({
        // A full page load, not a route change: nothing of this user stays in memory for the next one.
        next: () => window.location.assign('/auth/login')
      });
    });
  }
}