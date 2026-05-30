import { DatePipe } from '@angular/common';
import { TypingState } from '../../../state/messages/typing.state';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ConversationsState } from '../../../state/conversations/conversations.state';
import { AuthService } from '../../../core/auth/auth.service';
import { MessagesState } from '../../../state/messages/messages.state';
import { FormsModule } from '@angular/forms';
import { PresenceState } from '../../../state/presence/presence.state';
import { ChatSignalRService } from '../../../core/signalr/chat-signalr.service';
import { Subject, takeUntil } from 'rxjs';
import { FilesApiService } from '../../../core/services/files-api.service';
import { UploadFileResponse } from '../../files/models/upload-file-response';

import { Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
@Component({
  selector: 'app-conversation-chat',
  standalone: true,
  imports: [DatePipe, TranslateModule, FormsModule],
  template: `
  
<div class="grid h-full min-h-0 w-full grid-rows-[auto_1fr_auto] overflow-hidden bg-slate-950">
<header class="border-b border-white/10 px-6 py-4">
<div class="flex flex-row-reverse items-start gap-3">
<div
  class="flex h-11 w-11 items-center justify-center rounded-xl border-2 bg-blue-600 text-sm font-bold text-white"
  [class.border-emerald-400]="presenceState.isOnline(conversationId(), conversationPresenceUserId())"
  [class.border-slate-500]="!presenceState.isOnline(conversationId(), conversationPresenceUserId())"
>
  {{ conversation()?.displayTitle?.charAt(0) || '?' }}
</div>

  <div class="min-w-0">
    <h2 class="truncate text-lg font-bold text-white">
      {{ conversation()?.displayTitle || ('messages.title' | translate) }}
    </h2>


<p class="text-xs text-slate-400">
  @if (conversation()?.type === 1) {
    {{ 'presence.lastSeenRecently' | translate }}
  } @else {
    {{ 'conversations.type.group' | translate }}
  }
</p>



    <p class="text-xs text-slate-400">
      {{ conversation()?.type === 1 ? 'Private' : 'Group' }}
    </p>
  </div>
</div>
      </header>
@if (typingState.isAnyoneTyping(conversationId()!, currentUserId())) {
  <div class="shrink-0 border-b border-white/10 bg-slate-900/60 px-6 py-2 text-xs text-blue-400">
    {{ 'conversations.messages.typing' | translate }}
  </div>
}
<section
  #messagesContainer
  dir="ltr"
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
            <div class="whitespace-pre-wrap break-words text-start" dir="auto">
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
  
@if (selectedAttachments().length > 0) {
  <div class="mx-auto mb-3 flex w-full max-w-4xl flex-wrap gap-2">
    @for (attachment of selectedAttachments(); track attachment.fileId) {
      <div class="rounded-xl bg-slate-800 px-3 py-2 text-xs text-slate-200">
        📎 {{ attachment.fileName }}
      </div>
    }
  </div>
}

<form class="flex w-full items-end gap-3" (ngSubmit)="sendMessage()">
<label
  class="flex h-11 w-11 shrink-0 cursor-pointer items-center justify-center rounded-2xl bg-slate-800 text-slate-300 hover:bg-slate-700"
>
  📎
  <input
    type="file"
    class="hidden"
    (change)="onFileSelected($event)"
  />
</label>
  <textarea
  rows="1"
  name="messageText"
  [ngModel]="messageText()"
  (ngModelChange)="messageText.set($event)"
  (input)="resizeTextarea($event); handleTyping()"
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
  private readonly filesApi = inject(FilesApiService);

readonly selectedAttachments = signal<UploadFileResponse[]>([]);
readonly isUploadingFile = signal(false);
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private activeConversationId: string | null = null;
  private readonly conversationsState = inject(ConversationsState);
  private readonly chatSignalR = inject(ChatSignalRService);
  readonly typingState = inject(TypingState);
  readonly presenceState = inject(PresenceState);
  private typingStopTimer: ReturnType<typeof setTimeout> | null = null;
  private isTypingSent = false;
  readonly messageText = signal('');
  readonly conversation = computed(() => {
    const id = this.conversationId();

    if (!id) {
      return null;
    }

    return this.conversationsState.getById(id);
  });
readonly conversationPresenceUserId = computed(() => {
  const conversation = this.conversation();

  if (!conversation || conversation.type !== 1) {
    return null;
  }

  return conversation.otherParticipantUserId;
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
    void this.switchConversation(params.get('conversationId'));
  });


    this.chatSignalR.presenceSnapshot$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        console.log('presence.snapshot received:', event);
        this.presenceState.setSnapshot(
          event.conversationId,
          event.userIds
        );


console.log('conversation:', this.conversation());
console.log('conversationId:', this.conversationId());
console.log('otherParticipantUserId:', this.conversationPresenceUserId());
console.log('presence state:', this.presenceState.onlineUsersByConversation());
console.log(
  'is online:',
  this.presenceState.isOnline(
    this.conversationId(),
    this.conversationPresenceUserId()
  )
);


      });

    this.chatSignalR.presenceChanged$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        console.log('presence.changed received:', event);


        this.presenceState.setUserPresence(
          event.conversationId,
          event.userId,
          event.isOnline
        );
      });


    this.chatSignalR.messageCreated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        const currentConversationId = this.conversationId();

        if (event.conversationId === currentConversationId) {
          this.messagesState.handleMessageCreated(event);

          this.scrollToBottom();
          this.chatSignalR.markAsDelivered(event.conversationId, event.sequenceNumber);
        }
      });
    this.chatSignalR.messageEdited$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        if (event.conversationId === this.conversationId()) {
          this.messagesState.handleMessageEdited(event);
        }
      });

    this.chatSignalR.messageDeleted$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        if (event.conversationId === this.conversationId()) {
          this.messagesState.handleMessageDeleted(event);
        }
      });

    this.chatSignalR.typingStarted$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        if (event.conversationId === this.conversationId()) {
          this.typingState.startTyping(event.conversationId, event.userId);
        }
      });

    this.chatSignalR.typingStopped$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        if (event.conversationId === this.conversationId()) {
          this.typingState.stopTyping(event.conversationId, event.userId);
        }
      });


this.chatSignalR.reconnected$
  .pipe(takeUntil(this.destroy$))
  .subscribe(() => {
    const conversationId = this.conversationId();

    if (conversationId) {
      this.chatSignalR.joinConversation(conversationId);
      this.chatSignalR.startPresenceRenewTimer(conversationId);
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

    this.scrollToBottom();


    this.messageText.set('');
    if (conversationId && this.isTypingSent) {
      this.chatSignalR.stopTyping(conversationId);
      this.isTypingSent = false;
    }

    if (this.typingStopTimer) {
      clearTimeout(this.typingStopTimer);
      this.typingStopTimer = null;
    }
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
  this.chatSignalR.stopPresenceRenewTimer();

  if (this.activeConversationId) {
    this.chatSignalR.leaveConversation(this.activeConversationId);
    this.presenceState.clearConversation(this.activeConversationId);
  }

  this.destroy$.next();
  this.destroy$.complete();
}


private scrollToBottom(): void {
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      const element = this.messagesContainer?.nativeElement;

      if (!element) {
        return;
      }

      element.scrollTop = element.scrollHeight;
    });
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



  handleTyping(): void {
    const conversationId = this.conversationId();

    if (!conversationId) {
      return;
    }

    if (!this.isTypingSent) {
      this.chatSignalR.startTyping(conversationId);
      this.isTypingSent = true;
    }

    if (this.typingStopTimer) {
      clearTimeout(this.typingStopTimer);
    }

    this.typingStopTimer = setTimeout(() => {
      this.chatSignalR.stopTyping(conversationId);
      this.isTypingSent = false;
    }, 1500);
  }

private async switchConversation(newConversationId: string | null): Promise<void> {
  const oldConversationId = this.activeConversationId;

  this.chatSignalR.stopPresenceRenewTimer();

  if (oldConversationId && oldConversationId !== newConversationId) {
    console.log('Leaving conversation:', oldConversationId);

    await this.chatSignalR.leaveConversation(oldConversationId);
    this.presenceState.clearConversation(oldConversationId);
  }

  this.activeConversationId = newConversationId;
  this.conversationId.set(newConversationId);
  this.messagesState.clear();

  if (!newConversationId) {
    return;
  }

  this.messagesState.load(newConversationId)
    .pipe(takeUntil(this.destroy$))
    .subscribe(() => {
      this.scrollToBottom();
    });

  console.log('Joining conversation:', newConversationId);

  await this.chatSignalR.joinConversation(newConversationId);

  this.chatSignalR.startPresenceRenewTimer(newConversationId);
}

onFileSelected(event: Event): void {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];

  if (!file) {
    return;
  }

  this.isUploadingFile.set(true);

  this.filesApi.upload(file).subscribe({
    next: response => {
      this.selectedAttachments.set([
        ...this.selectedAttachments(),
        response
      ]);

      this.isUploadingFile.set(false);
      input.value = '';
    },
    error: () => {
      this.isUploadingFile.set(false);
      input.value = '';
    }
  });
}


}