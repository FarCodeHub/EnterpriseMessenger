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
import { AttachmentPreviewComponent } from '../../messages/components/attachment-preview/attachment-preview.component';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import imageCompression from 'browser-image-compression';
import { SelectedAttachment } from '../../files/models/selected-attachment';
import { HttpEventType } from '@angular/common/http';
import { MessageDto } from '../../messages/models/message.dto';
@Component({
  selector: 'app-conversation-chat',
  standalone: true,
  imports: [
    DatePipe,
    TranslateModule,
    FormsModule,
    AttachmentPreviewComponent
  ],
  template: `
  
<div class="relative grid h-full min-h-0 w-full grid-rows-[auto_1fr_auto] overflow-hidden bg-slate-950">
<header class="border-b border-white/10 px-6 py-4">
<div class="flex flex-row-reverse items-start gap-3">
<div
  class="flex h-11 w-11 items-center justify-center rounded-xl border-2 bg-blue-600 text-sm font-bold text-white"
  [class.border-emerald-400]="presenceState.isOnline(conversationId(), conversationPresenceUserId())"
  [class.border-slate-500]="!presenceState.isOnline(conversationId(), conversationPresenceUserId())">
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
  class="min-h-0 overflow-y-auto px-6 py-4"
  (scroll)="onMessagesScroll()"
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
  [id]="'message-' + message.id"
  class="group flex items-center gap-2"
  [class.justify-end]="message.senderUserId === currentUserId()"
  [class.justify-start]="message.senderUserId !== currentUserId()"
>

  







<div
  class="max-w-[70%] rounded-2xl text-sm shadow"
 [class.bg-blue-600]="message.senderUserId === currentUserId() && !isMediaOnlyMessage(message)"
[class.bg-slate-800]="message.senderUserId !== currentUserId() && !isMediaOnlyMessage(message)"
  [class.bg-slate-800]="message.senderUserId !== currentUserId()"
  [class.text-slate-100]="message.senderUserId !== currentUserId()"
[class.px-4]="!isMediaOnlyMessage(message)"
[class.py-3]="!isMediaOnlyMessage(message)"
  [class.overflow-hidden]="isImageOnlyMessage(message)"
>
                @if (message.deletedAtUtc) {
                  <div class="italic text-slate-300">
                    {{ 'messages.deleted' | translate }}
                  </div>
                } @else {
@if (message.body && message.body.trim().length > 0) {
  <div
    class="text-start"
    [attr.dir]="getTextDirection(message.body)"
  >

 


    @if (!isMediaOnlyMessage(message) && isLongTextMessage(message)) {
      <div class="mb-1 inline-flex items-center gap-1 text-[12px] leading-none opacity-70">
        <span>
          {{ message.createdAtUtc | date:'shortTime' }}
        </span>

        @if (message.senderUserId === currentUserId()) {
          <span
            class="font-bold tracking-[-0.18em]"
           [class.text-sky-300]="getReceiptStatus(message) === 'seen'"
          >
            {{ getReceiptIcon(message) }}
          </span>
        }
      </div>
@if (message.replyToMessageId) {
    <button
    type="button"
    class="mb-2 w-full rounded-lg border-s-4 border-blue-400 bg-black/20 px-3 py-2 text-start text-xs hover:bg-black/30"
    (click)="jumpToReplyMessage(message)"
  >
    @if (findReplyMessage(message.replyToMessageId); as repliedMessage) {
      <div class="font-semibold text-blue-300">
        {{ 'messages.replyingTo' | translate }}
      </div>

      <div class="mt-1 line-clamp-2 text-slate-200">
        {{ repliedMessage.body || '📎 Attachment' }}
      </div>
    } @else {
      <div class="font-semibold text-blue-300">
        {{ 'messages.replyingToMessage' | translate }}
        #{{ message.replyToSequenceNumber }}
      </div>
    }
  </button>
}
      <div class="whitespace-pre-wrap break-words">
        {{ message.body }}
      </div>
    } 
    
    
    @else {
      @if (!isMediaOnlyMessage(message)) {
        <span class="inline-flex items-center gap-1 pe-2 text-[12px] leading-none opacity-70 align-baseline">
          <span>
            {{ message.createdAtUtc | date:'shortTime' }}
          </span>

          @if (message.senderUserId === currentUserId()) {
            <span
              class="font-bold tracking-[-0.18em]"
             [class.text-sky-300]="getReceiptStatus(message) === 'seen'"
            >
              {{ getReceiptIcon(message) }}
            </span>
          }
        </span>
      }
@if (message.replyToMessageId) {
    <button
    type="button"
    class="mb-2 w-full rounded-lg border-s-4 border-blue-400 bg-black/20 px-3 py-2 text-start text-xs hover:bg-black/30"
    (click)="jumpToReplyMessage(message)"
  >
    @if (findReplyMessage(message.replyToMessageId); as repliedMessage) {
      <div class="font-semibold text-blue-300">
        {{ 'messages.replyingTo' | translate }}
      </div>

      <div class="mt-1 line-clamp-2 text-slate-200">
        {{ repliedMessage.body || '📎 Attachment' }}
      </div>
    } @else {
      <div class="font-semibold text-blue-300">
        {{ 'messages.replyingToMessage' | translate }}
        #{{ message.replyToSequenceNumber }}
      </div>
    }
  </button>
}
      <span class="whitespace-pre-wrap break-words">
        {{ message.body }}
      </span>
    }
  </div>
}
@if (message.attachments.length > 0) {
  <div
    class="relative overflow-hidden rounded-xl"
    [class.mt-2]="!isImageOnlyMessage(message)"
    [class.grid]="isImageAlbumMessage(message)"
    [class.grid-cols-2]="isImageAlbumMessage(message)"
    [class.gap-1]="isImageAlbumMessage(message)"
    [class.max-w-sm]="isImageAlbumMessage(message)"
  >
    @for (attachment of message.attachments; track attachment.id) {
      <div
        [class.aspect-square]="isImageAlbumMessage(message)"
        [class.overflow-hidden]="isImageAlbumMessage(message)"
      >
        <app-attachment-preview
          [attachment]="attachment"
          [compact]="isImageAlbumMessage(message)"
        />
      </div>
    }

    @if (isMediaOnlyMessage(message)) {
      <div class="absolute bottom-2 end-2 rounded-full bg-black/55 px-2 py-1 text-[11px] text-white">
        {{ message.createdAtUtc | date:'shortTime' }}
      @if (message.senderUserId === currentUserId()) {
  <span
    class="ms-1"
    [class.text-sky-300]="getReceiptStatus(message) === 'seen'"
  >
    {{ getReceiptIcon(message) }}
  </span>
}
      </div>
    }
  </div>
}
                }

@if (
  !isMediaOnlyMessage(message) &&
  (!message.body || message.body.trim().length === 0)
) {
  <div class="mt-2 flex items-center gap-1 text-[12px] opacity-70">
    @if (message.senderUserId === currentUserId()) {
      <span
        class="font-bold tracking-[-0.18em]"
        [class.text-sky-300]="getReceiptStatus(message) === 'seen'"
      >
        {{ getReceiptIcon(message) }}
      </span>
    }

    <span>
      {{ message.createdAtUtc | date:'shortTime' }}
    </span>
  </div>
}
              </div>


@if (message.senderUserId !== currentUserId()) {
  <div class="relative shrink-0 opacity-0 transition-opacity group-hover:opacity-100">
    <button
      type="button"
      class="flex h-8 w-8 items-center justify-center rounded-full bg-slate-700/90 text-slate-200 shadow hover:bg-slate-600"
      (click)="toggleActionMenu(message.id)"
    >
      ⋯
    </button>

    @if (openActionMenuMessageId() === message.id) {
      <div class="absolute bottom-10 start-0 z-30 min-w-36 overflow-hidden rounded-xl bg-slate-800 py-1 text-sm text-slate-100 shadow-xl">
        <button
          type="button"
          class="block w-full px-4 py-2 text-start hover:bg-white/10"
          (click)="replyToMessage(message)"
        >
          {{ 'conversations.messages.actions.reply' | translate }}
        </button>

        <button
          type="button"
          class="block w-full px-4 py-2 text-start hover:bg-white/10"
         
          [disabled]="!message.body || message.body.trim().length === 0"
          [class.opacity-50]="!message.body || message.body.trim().length === 0"
        >
          {{ 'conversations.messages.actions.copy' | translate }}
        </button>
      </div>
    }
  </div>
}


            </div>
          }
        </div>
      </section>

@if (shouldShowBackToLatest()) {
  <button
    type="button"
    class="absolute bottom-24 end-6 z-30 flex h-12 w-12 items-center justify-center rounded-full bg-slate-700 text-white shadow-xl hover:bg-slate-600"
    (click)="backToLatestMessages()"
  >
    @if ((conversation()?.unreadCount ?? 0) > 0) {
      <span class="absolute -top-2 -end-2 rounded-full bg-blue-500 px-2 py-0.5 text-xs font-bold">
        {{ conversation()?.unreadCount }}
      </span>
    }

    <span class="text-xl leading-none">⌄</span>
  </button>
}

<footer class="border-t border-white/10 bg-slate-900 px-4 py-3">
  
@if (selectedAttachments().length > 0) {
  <div class="mb-3 flex w-full flex-wrap gap-3">
    @for (attachment of selectedAttachments(); track attachment.fileId) {
      <div class="relative w-32 rounded-2xl bg-slate-800 p-2 shadow">
        <button
          type="button"
          class="absolute -end-2 -top-2 z-10 flex h-6 w-6 items-center justify-center rounded-full bg-red-600 text-xs text-white hover:bg-red-500"
          (click)="removeSelectedAttachment(attachment.fileId)"
        >
          ×
        </button>

        @if (attachment.localPreviewUrl) {
          <img
            class="h-24 w-full rounded-xl object-cover"
            [src]="attachment.localPreviewUrl"
            [alt]="attachment.fileName"
          />
        } @else {
          <div class="flex h-24 w-full items-center justify-center rounded-xl bg-slate-700 px-2 text-center text-xs text-slate-300">
            📎 {{ attachment.fileName }}
          </div>
        }

        <!-- <div class="mt-2 truncate text-xs text-slate-300">
          {{ attachment.fileName }}
        </div> -->

        @if (attachment.isUploading) {
          <div class="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-700">
            <div
              class="h-full rounded-full bg-blue-500"
              [style.width.%]="attachment.progress"
            ></div>
          </div>
        }

        @if (!attachment.isUploading && !attachment.error) {
          <div class="mt-2 text-center text-xs text-emerald-400">
            ✓ Uploaded
          </div>
        }

        @if (attachment.error) {
          <div class="mt-2 text-center text-xs text-red-400">
            Upload failed
          </div>
        }
      </div>
    }
  </div>
}



@if (replyingToMessage()) {
  <div class="mb-3 w-full overflow-hidden rounded-xl border-s-4 border-blue-500 bg-slate-800 px-4 py-3">
    <div class="flex min-w-0 items-start justify-between gap-3">
      <div class="min-w-0 flex-1">
        <div class="text-xs font-semibold text-blue-400">
          {{ 'messages.replyingTo' | translate }}
        </div>

        <div
          class="mt-1 max-h-10 overflow-hidden break-words text-sm leading-5 text-slate-300"
          dir="auto"
        >
          {{ replyingToMessage()?.body || '📎 Attachment' }}
        </div>
      </div>

      <button
        type="button"
        class="shrink-0 text-slate-400 hover:text-white"
        (click)="cancelReply()"
      >
        ×
      </button>
    </div>
  </div>
}





<form class="flex w-full items-end gap-3" (ngSubmit)="sendMessage()">
<label
  class="flex h-11 w-11 shrink-0 cursor-pointer items-center justify-center rounded-2xl bg-slate-800 text-slate-300 hover:bg-slate-700">
  📎
<input
  type="file"
  class="hidden"
  multiple
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
class="max-h-40 min-h-11 min-w-0 flex-1 resize-none overflow-hidden rounded-2xl border border-white/10 bg-slate-800 px-4 py-3 text-sm text-white outline-none placeholder:text-slate-500 focus:border-blue-500"  [placeholder]="'conversations.messages.input.placeholder' | translate"></textarea>

<button
  type="submit"
  [disabled]="hasUploadingAttachments()"
  [class.opacity-50]="hasUploadingAttachments()"
[class.cursor-not-allowed]="hasUploadingAttachments()"
  class="rounded-2xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-500">
  {{ 'conversations.messages.input.send' | translate }}
</button>
  </form>
</footer>
@if (isSendModeOpen()) {
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
    <div class="w-full max-w-sm rounded-2xl bg-slate-900 p-5 text-white shadow-2xl">
      <h3 class="text-lg font-bold">
        {{ 'attachments.sendMode.title' | translate }}
      </h3>

      <p class="mt-2 text-sm text-slate-400">
        {{ pendingImageFile()?.name }}
      </p>

      <div class="mt-5 space-y-3">
        <button
          type="button"
          class="w-full rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold hover:bg-blue-500"
          (click)="sendPendingImageAsPhoto()"
        >
          {{ 'attachments.sendMode.asPhoto' | translate }}
        </button>

        <button
          type="button"
          class="w-full rounded-xl bg-slate-800 px-4 py-3 text-sm font-semibold hover:bg-slate-700"
          (click)="sendPendingImageAsFile()"
        >
          {{ 'attachments.sendMode.asFile' | translate }}
        </button>

        <button
          type="button"
          class="w-full rounded-xl px-4 py-3 text-sm text-slate-400 hover:bg-white/5"
          (click)="cancelSendMode()"
        >
          {{ 'common.cancel' | translate }}
        </button>
      </div>
    </div>
  </div>
}
    </div>
  `
})
export class ConversationChatComponent implements OnInit, OnDestroy {
  readonly openActionMenuMessageId = signal<string | null>(null);
  readonly isAwayFromBottom = signal(false);
readonly shouldShowBackToLatest = computed(() => {
  const conversation = this.conversation();

  return this.isViewingAroundMessages() ||
    this.isAwayFromBottom() ||
    (conversation?.unreadCount ?? 0) > 0;
});
  private lastDeliveredSequenceNumber: number | null = null;
  private lastSeenSequenceNumber: number | null = null;
  private readonly destroy$ = new Subject<void>();
  private readonly filesApi = inject(FilesApiService);
  readonly pendingImageFile = signal<File | null>(null);
  readonly isSendModeOpen = signal(false);
  readonly selectedAttachments = signal<SelectedAttachment[]>([]);
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
  readonly isViewingAroundMessages = signal(false);
  readonly messageText = signal('');
  readonly conversation = computed(() => {
    const id = this.conversationId();

    if (!id) {
      return null;
    }

    return this.conversationsState.getById(id);
  });
  readonly hasUploadingAttachments = computed(() =>
    this.selectedAttachments().some(x => x.isUploading)
  );
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
  readonly replyingToMessage = signal<MessageDto | null>(null);


  @ViewChild('messagesContainer')
  private messagesContainer?: ElementRef<HTMLDivElement>;
  ngOnInit(): void {

    document.addEventListener(
      'visibilitychange',
      this.handleVisibilityChange
    );

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
          this.markCurrentConversationAsDelivered();
          this.markCurrentConversationAsSeen();
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



    this.chatSignalR.messageDelivered$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        const conversation = this.conversation();

        if (
          conversation &&
          event.conversationId === conversation.id &&
          event.userId === conversation.otherParticipantUserId
        ) {
          this.conversationsState.updateOtherParticipantDelivered(
            event.conversationId,
            event.sequenceNumber
          );
        }
      });

    this.chatSignalR.messageSeen$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        const conversation = this.conversation();

        if (
          conversation &&
          event.conversationId === conversation.id &&
          event.userId === conversation.otherParticipantUserId
        ) {
          this.conversationsState.updateOtherParticipantSeen(
            event.conversationId,
            event.sequenceNumber
          );
        }
      });





  }

  private readonly handleVisibilityChange = (): void => {
    if (document.visibilityState === 'visible') {
      this.markCurrentConversationAsSeenIfAllowed();
    }
  };


  toggleActionMenu(messageId: string): void {
  this.openActionMenuMessageId.update(current =>
    current === messageId ? null : messageId
  );
}

closeActionMenu(): void {
  this.openActionMenuMessageId.set(null);
}

replyToMessage(message: MessageDto): void {
  this.startReply(message);
  this.closeActionMenu();
}

  findReplyMessage(replyToMessageId: string | null): MessageDto | null {
    if (!replyToMessageId) {
      return null;
    }

    return this.messagesState.messages()
      .find(message => message.id === replyToMessageId) ?? null;
  }
  startReply(message: MessageDto): void {
    this.replyingToMessage.set(message);
  }

  cancelReply(): void {
    this.replyingToMessage.set(null);
  }
  sendMessage(): void {
    const conversationId = this.conversationId();
    const rawBody = this.messageText().trim();

    const attachments = this.selectedAttachments()
      .filter(x => !x.isUploading && !x.error)
      .map(file => ({
        fileId: file.fileId,
        fileName: file.fileName,
        contentType: file.contentType,
        size: file.size,
        storagePath: file.storagePath
      }));

    const hasAttachments = attachments.length > 0;

    if (!conversationId || (!rawBody && !hasAttachments)) {
      return;
    }

    if (this.hasUploadingAttachments()) {
      return;
    }



    this.messagesState.send({
      conversationId,
      clientMessageId: crypto.randomUUID(),
      body: rawBody,
      replyToMessageId: this.replyingToMessage()?.id ?? null,
      attachments
    });

    this.messageText.set('');
    this.selectedAttachments.set([]);
    this.replyingToMessage.set(null);
    if (conversationId && this.isTypingSent) {
      this.chatSignalR.stopTyping(conversationId);
      this.isTypingSent = false;
    }

    if (this.typingStopTimer) {
      clearTimeout(this.typingStopTimer);
      this.typingStopTimer = null;
    }

    this.scrollToBottom();
  }
  ngOnDestroy(): void {
    this.chatSignalR.stopPresenceRenewTimer();

    if (this.activeConversationId) {
      this.chatSignalR.leaveConversation(this.activeConversationId);
      this.presenceState.clearConversation(this.activeConversationId);
    }
    document.removeEventListener(
      'visibilitychange',
      this.handleVisibilityChange
    );
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
        this.isAwayFromBottom.set(false);
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

    this.lastDeliveredSequenceNumber = null;
    this.lastSeenSequenceNumber = null;

    if (!newConversationId) {
      return;
    }

    this.messagesState.load(newConversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.scrollToBottom();

        requestAnimationFrame(() => {
          this.markCurrentConversationAsDelivered();
          this.markCurrentConversationAsSeenIfAllowed();
        });
      });

    await this.chatSignalR.joinConversation(newConversationId);

    this.chatSignalR.startPresenceRenewTimer(newConversationId);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);

    input.value = '';

    if (files.length === 0) {
      return;
    }

    files.forEach(file => {
      if (file.type.startsWith('image/')) {
        // فعلاً پیش‌فرض Send as Photo
        void this.compressAndUploadImage(file);
        return;
      }

      this.uploadAttachment(file);
    });
  }


  private async compressAndUploadImage(file: File): Promise<void> {
    const tempId = this.addPendingAttachment(file);

    try {

      const compressedFile = await imageCompression(file, {
        maxSizeMB: 1,
        maxWidthOrHeight: 1600,
        useWebWorker: true,
        fileType: 'image/jpeg',
        initialQuality: 0.8
      });

      const renamedFile = new File(
        [compressedFile],
        file.name,
        {
          type: compressedFile.type,
          lastModified: Date.now()
        }
      );

      this.uploadAttachmentByTempId(tempId, renamedFile);
    } catch {
      this.updateSelectedAttachment(tempId, {
        isUploading: false,
        error: 'Compression failed'
      });
    }
  }


  sendPendingImageAsFile(): void {
    const file = this.pendingImageFile();

    if (!file) {
      return;
    }

    this.isSendModeOpen.set(false);
    this.pendingImageFile.set(null);

    this.uploadAttachment(file);
  }

  async sendPendingImageAsPhoto(): Promise<void> {
    const file = this.pendingImageFile();

    if (!file) {
      return;
    }

    this.isSendModeOpen.set(false);
    this.pendingImageFile.set(null);

    const compressedFile = await imageCompression(file, {
      maxSizeMB: 1,
      maxWidthOrHeight: 1600,
      useWebWorker: true,
      fileType: 'image/jpeg',
      initialQuality: 0.8
    });

    this.uploadAttachment(compressedFile);
  }

  cancelSendMode(): void {
    this.isSendModeOpen.set(false);
    this.pendingImageFile.set(null);
  }


  private uploadAttachment(file: File): void {
    const tempId = this.addPendingAttachment(file);
    this.uploadAttachmentByTempId(tempId, file);
  }



  private updateSelectedAttachment(
    fileId: string,
    patch: Partial<SelectedAttachment>
  ): void {
    this.selectedAttachments.set(
      this.selectedAttachments().map(item =>
        item.fileId === fileId
          ? { ...item, ...patch }
          : item
      )
    );
  }
  removeSelectedAttachment(fileId: string): void {
    const attachment = this.selectedAttachments()
      .find(x => x.fileId === fileId);

    if (attachment?.localPreviewUrl) {
      URL.revokeObjectURL(attachment.localPreviewUrl);
    }

    this.selectedAttachments.set(
      this.selectedAttachments().filter(x => x.fileId !== fileId)
    );
  }

  isImageOnlyMessage(message: MessageDto): boolean {
    return (
      message.attachments.length > 0 &&
      message.attachments.every(x => x.contentType.startsWith('image/')) &&
      (!message.body || message.body.trim().length === 0)
    );
  }
  private addPendingAttachment(file: File): string {
    const tempId = crypto.randomUUID();

    const localPreviewUrl = file.type.startsWith('image/')
      ? URL.createObjectURL(file)
      : undefined;

    this.selectedAttachments.set([
      ...this.selectedAttachments(),
      {
        fileId: tempId,
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        size: file.size,
        storagePath: '',
        progress: 0,
        isUploading: true,
        error: null,
        localPreviewUrl
      }
    ]);

    return tempId;
  }

  private uploadAttachmentByTempId(tempId: string, file: File): void {
    this.filesApi.uploadWithProgress(file).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress) {
          const progress = event.total
            ? Math.round((event.loaded / event.total) * 100)
            : 0;

          this.updateSelectedAttachment(tempId, { progress });
        }

        if (event.type === HttpEventType.Response && event.body) {
          this.updateSelectedAttachment(tempId, {
            fileId: event.body.fileId,
            fileName: event.body.fileName,
            contentType: event.body.contentType,
            size: event.body.size,
            storagePath: event.body.storagePath,
            progress: 100,
            isUploading: false,
            error: null
          });
        }
      },
      error: () => {
        this.updateSelectedAttachment(tempId, {
          isUploading: false,
          error: 'Upload failed'
        });
      }
    });
  }
  isImageAlbumMessage(message: MessageDto): boolean {
    return (
      message.attachments.length > 1 &&
      message.attachments.every(x => x.contentType.startsWith('image/'))
    );
  }
  isMediaOnlyMessage(message: MessageDto): boolean {
    return (
      message.attachments.length > 0 &&
      message.attachments.every(x => x.contentType.startsWith('image/')) &&
      (!message.body || message.body.trim().length === 0)
    );
  }


  getReceiptIcon(message: MessageDto): string {
    const status = this.getReceiptStatus(message);

    if (status === 'failed') {
      return '!';
    }

    if (status === 'sending') {
      return '◷';
    }

    if (status === 'seen') {
      return '✓✓';
    }

    if (status === 'delivered') {
      return '✓✓';
    }

    return '✓';
  }
  getReceiptStatus(message: MessageDto): 'sending' | 'failed' | 'sent' | 'delivered' | 'seen' {
    if (message.receiptStatus === 'sending') {
      return 'sending';
    }

    if (message.receiptStatus === 'failed') {
      return 'failed';
    }

    if (message.senderUserId !== this.currentUserId()) {
      return 'sent';
    }

    const conversation = this.conversation();

    if (!conversation || conversation.type !== 1) {
      return 'sent';
    }

    const seenCursor =
      conversation.otherParticipantLastReadSequenceNumber ?? 0;

    const deliveredCursor =
      conversation.otherParticipantLastDeliveredSequenceNumber ?? 0;

    if (message.sequenceNumber <= seenCursor) {
      return 'seen';
    }

    if (message.sequenceNumber <= deliveredCursor) {
      return 'delivered';
    }

    return 'sent';
  }


  private markCurrentConversationAsSeen(): void {
    const conversationId = this.conversationId();
    const lastSequenceNumber = this.messagesState.getLastSequenceNumber();

    if (!conversationId || !lastSequenceNumber) {
      return;
    }

    this.chatSignalR.markAsSeen(conversationId, lastSequenceNumber);
  }

  private isNearBottom(): boolean {
    const element = this.messagesContainer?.nativeElement;

    if (!element) {
      return false;
    }

    const threshold = 120;

    return element.scrollHeight - element.scrollTop - element.clientHeight < threshold;
  }

  private isDocumentVisible(): boolean {
    return document.visibilityState === 'visible';
  }

  private markCurrentConversationAsDelivered(): void {
    const conversationId = this.conversationId();
    const lastSequenceNumber = this.messagesState.getLastSequenceNumber();

    if (!conversationId || !lastSequenceNumber) {
      return;
    }

    if (
      this.lastDeliveredSequenceNumber !== null &&
      lastSequenceNumber <= this.lastDeliveredSequenceNumber
    ) {
      return;
    }

    this.lastDeliveredSequenceNumber = lastSequenceNumber;
    this.chatSignalR.markAsDelivered(conversationId, lastSequenceNumber);
  }

  private markCurrentConversationAsSeenIfAllowed(): void {
    const conversationId = this.conversationId();
    const lastSequenceNumber = this.messagesState.getLastSequenceNumber();

    if (!conversationId || !lastSequenceNumber) {
      return;
    }

    if (!this.isDocumentVisible()) {
      return;
    }

    if (!this.isNearBottom()) {
      return;
    }

    if (
      this.lastSeenSequenceNumber !== null &&
      lastSequenceNumber <= this.lastSeenSequenceNumber
    ) {
      return;
    }

    this.lastSeenSequenceNumber = lastSequenceNumber;
    this.chatSignalR.markAsSeen(conversationId, lastSequenceNumber);
  }
onMessagesScroll(): void {
  this.isAwayFromBottom.set(!this.isNearBottom());
  this.markCurrentConversationAsSeenIfAllowed();
}


  getTextDirection(text: string | null | undefined): 'rtl' | 'ltr' {
    if (!text) {
      return 'rtl';
    }

    const rtlRegex = /[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF]/;

    return rtlRegex.test(text) ? 'rtl' : 'ltr';
  }

  isLongTextMessage(message: MessageDto): boolean {
    const body = message.body?.trim() ?? '';

    return body.includes('\n') || body.length > 45;
  }

  scrollToMessage(messageId: string | null): void {
    if (!messageId) {
      return;
    }

    const element = document.getElementById(`message-${messageId}`);

    if (!element) {
      return;
    }

    element.scrollIntoView({
      behavior: 'smooth',
      block: 'center'
    });

    element.classList.add('ring-2', 'ring-blue-400');

    setTimeout(() => {
      element.classList.remove('ring-2', 'ring-blue-400');
    }, 1200);
  }
  private scrollToMessageById(messageId: string): void {
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        const element = document.getElementById(`message-${messageId}`);

        if (!element) {
          return;
        }

        element.scrollIntoView({
          behavior: 'smooth',
          block: 'center'
        });

        element.classList.add('ring-2', 'ring-blue-400');

        setTimeout(() => {
          element.classList.remove('ring-2', 'ring-blue-400');
        }, 1200);
      });
    });
  }

  jumpToReplyMessage(message: MessageDto): void {
    if (!message.replyToMessageId || !message.replyToSequenceNumber) {
      return;
    }

    const existingElement = document.getElementById(
      `message-${message.replyToMessageId}`
    );

    if (existingElement) {
      this.scrollToMessageById(message.replyToMessageId);
      return;
    }

    this.messagesState
      .loadAround(
        message.conversationId,
        message.replyToSequenceNumber,
        50
      )
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.isViewingAroundMessages.set(true);
        this.scrollToMessageById(message.replyToMessageId!);
      });
  }
  backToLatestMessages(): void {
    const conversationId = this.conversationId();

    if (!conversationId) {
      return;
    }

    this.messagesState.load(conversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.isViewingAroundMessages.set(false);
        this.scrollToBottom();
        this.markCurrentConversationAsDelivered();
        this.markCurrentConversationAsSeenIfAllowed();
      });
  }
}