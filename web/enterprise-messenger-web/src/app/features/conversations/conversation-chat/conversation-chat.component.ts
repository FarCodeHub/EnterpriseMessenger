import { DatePipe } from '@angular/common';

import { ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ConversationsState } from '../../../state/conversations/conversations.state';
import { AuthService } from '../../../core/auth/auth.service';
import { MessagesState } from '../../../state/messages/messages.state';
import { FormsModule } from '@angular/forms';

import { ChatSignalRService } from '../../../core/signalr/chat-signalr.service';

import { Subject, takeUntil } from 'rxjs';

import { Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
@Component({
  selector: 'app-conversation-chat',
  standalone: true,
  imports: [DatePipe, TranslateModule, FormsModule],
  template: `
  
<div class="grid h-full min-h-0 w-full grid-rows-[auto_1fr_auto] overflow-hidden bg-slate-950">
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

<section
  #messagesContainer
  class="min-h-0 overflow-y-auto px-6 py-4 [scrollbar-width:thin] [scrollbar-color:#334155_transparent]"
>
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
  <form class="flex w-full items-end gap-3" (ngSubmit)="sendMessage()">
<textarea
  rows="1"
  name="messageText"
  [ngModel]="messageText()"
  (ngModelChange)="messageText.set($event)"
  (input)="resizeTextarea($event)"
  (keydown.enter)="handleEnter($event)"
class="max-h-40 min-h-11 min-w-0 flex-1 resize-none overflow-hidden rounded-2xl border border-white/10 bg-slate-800 px-4 py-3 text-sm text-white outline-none placeholder:text-slate-500 focus:border-blue-500"  [placeholder]="'conversations.messages.input.placeholder' | translate"
></textarea>

<button
  type="submit"
  class="rounded-2xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-500"
>
  {{ 'conversations.messages.input.send' | translate }}
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

  readonly conversationId = signal<string | null>(null);

  readonly currentUserId = computed(() => {
    return this.authService.user()?.id;
  });
@ViewChild('messagesContainer')
private messagesContainer?: ElementRef<HTMLDivElement>;
  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const conversationId = params.get('conversationId');

        this.conversationId.set(conversationId);
        this.messagesState.clear();

        if (!conversationId) {
          return;
        }

        this.messagesState.load(conversationId);
        setTimeout(() => this.scrollToBottom(), 300);
        this.chatSignalR.joinConversation(conversationId);
      });

    this.chatSignalR.messageCreated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        const currentConversationId = this.conversationId();

        if (event.conversationId === currentConversationId) {
          this.messagesState.handleMessageCreated(event);
          this.scrollToBottom();
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

  setTimeout(() => {
    const textarea = document.querySelector(
      'textarea[name="messageText"]'
    ) as HTMLTextAreaElement | null;

    if (textarea) {
      textarea.style.height = 'auto';
    }
  });
}

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }


private scrollToBottom(): void {
  queueMicrotask(() => {
    const element = this.messagesContainer?.nativeElement;

    if (!element) {
      return;
    }

    element.scrollTop = element.scrollHeight;
  });
}

handleEnter(event: Event): void {
  const keyboardEvent = event as KeyboardEvent;

  if (keyboardEvent.shiftKey) {
    return;
  }

  keyboardEvent.preventDefault();
  this.sendMessage();
}
resizeTextarea(event: Event): void {
  const textarea = event.target as HTMLTextAreaElement;

  textarea.style.height = 'auto';
  textarea.style.height = `${Math.min(textarea.scrollHeight, 160)}px`;
}
}