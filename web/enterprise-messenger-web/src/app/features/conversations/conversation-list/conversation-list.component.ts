import { DatePipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { Router } from '@angular/router';
import { ConversationsState } from '../../../state/conversations/conversations.state';

@Component({
  selector: 'app-conversation-list',
  standalone: true,
  imports: [DatePipe, TranslateModule],
  template: `
    <div class="space-y-2">
      @if (state.isLoading()) {
        <div class="rounded-xl bg-white/5 p-4 text-sm text-slate-400">
          {{ 'conversations.list.loading' | translate }}
        </div>
      }

      @if (!state.isLoading() && state.conversations().length === 0) {
        <div class="rounded-xl border border-dashed border-white/10 p-4 text-sm text-slate-400">
          {{ 'conversations.list.empty' | translate }}
        </div>
      }

@for (conversation of state.conversations(); track conversation.id) {
  <button
    type="button"
    class="w-full rounded-xl px-4 py-3 text-start hover:bg-white/10"
    (click)="openConversation(conversation.id)"
  >
    <div class="flex items-start gap-3">
      <div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-blue-600 text-sm font-bold text-white">
        {{ conversation.displayTitle.charAt(0) }}
      </div>

      <div class="min-w-0 flex-1">
        <div class="flex items-center justify-between gap-2">
          <div class="truncate font-semibold text-white">
            {{ conversation.displayTitle }}
          </div>

          @if (conversation.lastMessageAtUtc) {
            <div class="shrink-0 text-[11px] text-slate-500">
              {{ conversation.lastMessageAtUtc | date:'shortTime' }}
            </div>
          }
        </div>

        <div class="mt-1 flex items-center justify-between gap-2">
          <div class="min-w-0 truncate text-sm text-slate-400">
            @if (conversation.lastMessageSenderDisplayName) {
              <span class="text-slate-300">
                {{ conversation.lastMessageSenderDisplayName }}:
              </span>
            }

            {{ conversation.lastMessagePreview || ('conversations.list.noMessages' | translate) }}
          </div>

          @if (conversation.unreadCount > 0) {
            <span class="shrink-0 rounded-full bg-blue-600 px-2 py-0.5 text-xs font-bold text-white">
              {{ conversation.unreadCount }}
            </span>
          }
        </div>

        <div class="mt-1 flex items-center gap-2 text-[11px] text-slate-500">
          @if (conversation.isPinned) {
            <span>📌</span>
          }

          @if (conversation.isMuted) {
            <span>🔇</span>
          }

          @if (conversation.isArchived) {
            <span>{{ 'conversations.list.archived' | translate }}</span>
          }
        </div>
      </div>
    </div>
  </button>
}
    
    
    
    
    
    </div>
  `
})
export class ConversationListComponent implements OnInit {
  readonly state = inject(ConversationsState);

  ngOnInit(): void {
    this.state.load();
  }
  private readonly router = inject(Router);

openConversation(conversationId: string): void {
  this.router.navigate(['/app/conversations', conversationId]);
}
}