import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

import { AuthService } from '../../../core/auth/auth.service';
import { MessagesState } from '../../../state/messages/messages.state';

@Component({
  selector: 'app-conversation-chat',
  standalone: true,
  imports: [DatePipe, TranslateModule],
  template: `
    <div class="flex h-full min-h-0 flex-col bg-slate-950">
      <header class="border-b border-white/10 px-6 py-4">
        <h2 class="text-lg font-bold text-white">
          {{ 'messages.title' | translate }}
        </h2>
        <p class="text-xs text-slate-400">
          {{ conversationId() }}
        </p>
      </header>

      <section class="min-h-0 flex-1 overflow-y-auto px-6 py-4">
        @if (messagesState.isLoading()) {
          <div class="text-sm text-slate-400">
            {{ 'messages.loading' | translate }}
          </div>
        }

        @if (!messagesState.isLoading() && messagesState.messages().length === 0) {
          <div class="flex h-full items-center justify-center text-sm text-slate-500">
            {{ 'messages.empty' | translate }}
          </div>
        }

        <div class="space-y-3">
          @for (message of messagesState.messages(); track message.id) {
            <div
              class="flex"
              [class.justify-end]="message.senderUserId === currentUserId()"
              [class.justify-start]="message.senderUserId !== currentUserId()"
            >
              <div
                class="max-w-[70%] rounded-2xl px-4 py-3 text-sm shadow"
                [class.bg-blue-600]="message.senderUserId === currentUserId()"
                [class.text-white]="message.senderUserId === currentUserId()"
                [class.bg-slate-800]="message.senderUserId !== currentUserId()"
                [class.text-slate-100]="message.senderUserId !== currentUserId()"
              >
                @if (message.deletedAtUtc) {
                  <div class="italic text-slate-300">
                    {{ 'messages.deleted' | translate }}
                  </div>
                } @else {
                  <div class="whitespace-pre-wrap break-words">
                    {{ message.body }}
                  </div>

                  @if (message.attachments.length > 0) {
                    <div class="mt-2 space-y-1">
                      @for (attachment of message.attachments; track attachment.id) {
                        <div class="rounded-lg bg-black/20 px-3 py-2 text-xs">
                          📎 {{ attachment.fileName }}
                        </div>
                      }
                    </div>
                  }
                }

                <div class="mt-2 text-[11px] opacity-70">
                  {{ message.createdAtUtc | date:'shortTime' }}

                  @if (message.editedAtUtc) {
                    · {{ 'messages.edited' | translate }}
                  }
                </div>
              </div>
            </div>
          }
        </div>
      </section>
    </div>
  `
})
export class ConversationChatComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);

  readonly messagesState = inject(MessagesState);

  readonly conversationId = computed(() => {
    return this.route.snapshot.paramMap.get('conversationId');
  });

  readonly currentUserId = computed(() => {
    return this.authService.user()?.id;
  });

  ngOnInit(): void {
    const conversationId = this.conversationId();

    if (conversationId) {
      this.messagesState.load(conversationId);
    }
  }
}