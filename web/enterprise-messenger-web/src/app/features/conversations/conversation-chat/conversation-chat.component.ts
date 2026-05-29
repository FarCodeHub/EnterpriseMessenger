import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ConversationsState } from '../../../state/conversations/conversations.state';
import { AuthService } from '../../../core/auth/auth.service';
import { MessagesState } from '../../../state/messages/messages.state';
import { FormsModule } from '@angular/forms';
import { signal } from '@angular/core';
import { ChatSignalRService } from '../../../core/signalr/chat-signalr.service';
import { OnDestroy } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
@Component({
  selector: 'app-conversation-chat',
  standalone: true,
  imports: [DatePipe, TranslateModule, FormsModule],
  template: `
    <div class="flex h-full min-h-0 flex-col bg-slate-950">
      <header class="border-b border-white/10 px-6 py-4">
<div class="flex items-center gap-3">
  <div class="flex h-10 w-10 items-center justify-center rounded-full bg-blue-600 text-sm font-bold text-white">
    {{ conversation()?.displayTitle?.charAt(0) || '?' }}
  </div>

  <div class="min-w-0">
    <h2 class="truncate text-lg font-bold text-white">
      {{ conversation()?.displayTitle || ('messages.title' | translate) }}
    </h2>

    <p class="text-xs text-slate-400">
      {{ conversation()?.type === 1 ? 'Private' : 'Group' }}
    </p>
  </div>
</div>
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

  @if (message.status === 'sending') {
    · ...
  }

  @if (message.status === 'failed') {
    · {{ 'messages.failed' | translate }}
  }

  @if (message.editedAtUtc) {
    · {{ 'messages.edited' | translate }}
  }
</div>
              </div>
            </div>
          }
        </div>
      </section>



<footer class="border-t border-white/10 bg-slate-900 px-4 py-3">
<form class="flex items-end gap-3" (ngSubmit)="sendMessage()">
    <textarea
  rows="1"
  [ngModel]="messageText()"
  (ngModelChange)="messageText.set($event)"
  name="messageText"
  class="max-h-32 min-h-11 flex-1 resize-none rounded-2xl border border-white/10 bg-slate-800 px-4 py-3 text-sm text-white outline-none placeholder:text-slate-500 focus:border-blue-500"
  [placeholder]="'messages.input.placeholder' | translate"
></textarea>

<button
  type="submit"
  class="rounded-2xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-500"
>
  {{ 'messages.input.send' | translate }}
</button>
  </form>
</footer>


    </div>
  `
})
export class ConversationChatComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly conversationsState = inject(ConversationsState);
  private readonly chatSignalR = inject(ChatSignalRService);
  readonly messageText = signal('');
  readonly conversation = computed(() => {
    const id = this.conversationId();

    if (!id) {
      return null;
    }

    return this.conversationsState.getById(id);
  });
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

    this.chatSignalR.joinConversation(conversationId);
      this.chatSignalR.messageCreated$
  .pipe(takeUntil(this.destroy$))
  .subscribe(event => {
    if (event.conversationId === conversationId) {
      this.messagesState.handleMessageCreated(event);
    }
  });
  }
 
this.chatSignalR.messageCreated$
  .pipe(takeUntil(this.destroy$))
  .subscribe(event => {
    console.log('message.created received:', event);

    if (event.conversationId === conversationId) {
      this.messagesState.handleMessageCreated(event);
    }
  });
}


  sendMessage(): void {
    const conversationId = this.conversationId();
    const body = this.messageText().trim();

    if (!conversationId || !body) {
      return;
    }

    this.messagesState.send({
      conversationId,
      clientMessageId: crypto.randomUUID(),
      body,
      replyToMessageId: null,
      attachments: []
    });

    this.messageText.set('');
  }

ngOnDestroy(): void {
  this.destroy$.next();
  this.destroy$.complete();
}
}