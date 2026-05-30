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
  
<div class="grid h-full min-h-0 w-full grid-rows-[auto_1fr_auto] overflow-hidden bg-slate-950">
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
  class="min-h-0 overflow-y-auto px-6 py-4 [scrollbar-width:thin] [scrollbar-color:#334155_transparent]">
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
  <div class="whitespace-pre-wrap break-words text-start" dir="auto">
    {{ message.body }}
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
    [class.text-sky-300]="message.receiptStatus === 'seen'"
  >
    {{ getReceiptIcon(message) }}
  </span>
}
      </div>
    }
  </div>
}
                }

@if (!isImageOnlyMessage(message)) {
  <div class="mt-2 text-[11px] opacity-70">
    {{ message.createdAtUtc | date:'shortTime' }}

@if (message.senderUserId === currentUserId()) {
  <span
    class="ms-1"
    [class.text-sky-300]="message.receiptStatus === 'seen'"
  >
    {{ getReceiptIcon(message) }}
  </span>
}
  </div>
}
              </div>
            </div>
          }
        </div>
      </section>



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
    if (event.conversationId === this.conversationId()) {
      this.messagesState.handleMessageDelivered(
        event.conversationId,
        event.sequenceNumber
      );
    }
  });

this.chatSignalR.messageSeen$
  .pipe(takeUntil(this.destroy$))
  .subscribe(event => {
    if (event.conversationId === this.conversationId()) {
      this.messagesState.handleMessageSeen(
        event.conversationId,
        event.sequenceNumber
      );
    }
  });





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
console.log('message attachments payload:', attachments);
  

  this.messagesState.send({
    conversationId,
    clientMessageId: crypto.randomUUID(),
    body:rawBody,
    replyToMessageId: null,
    attachments
  });

  this.messageText.set('');
  this.selectedAttachments.set([]);

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
    this.markCurrentConversationAsDelivered();
    this.markCurrentConversationAsSeen();
  });

    console.log('Joining conversation:', newConversationId);

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
  if ( message.receiptStatus === 'failed') {
    return '❌';
  }

  if (message.receiptStatus === 'sending') {
    return '⏳';
  }

  if (message.receiptStatus === 'seen') {
    return '✓✓';
  }

  if (message.receiptStatus === 'delivered') {
    return '✓✓';
  }

  return '✓';
}

private markCurrentConversationAsDelivered(): void {
  const conversationId = this.conversationId();
  const lastSequenceNumber = this.messagesState.getLastSequenceNumber();

  if (!conversationId || !lastSequenceNumber) {
    return;
  }

  this.chatSignalR.markAsDelivered(conversationId, lastSequenceNumber);
}

private markCurrentConversationAsSeen(): void {
  const conversationId = this.conversationId();
  const lastSequenceNumber = this.messagesState.getLastSequenceNumber();

  if (!conversationId || !lastSequenceNumber) {
    return;
  }

  this.chatSignalR.markAsSeen(conversationId, lastSequenceNumber);
}



}