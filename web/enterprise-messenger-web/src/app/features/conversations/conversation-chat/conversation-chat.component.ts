import { DatePipe } from '@angular/common';
import { TypingState } from '../../../state/messages/typing.state';
import { ActivatedRoute, Router } from '@angular/router';
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
import { MessagesApiService } from '../../../core/services/messages-api.service';
import { OfflineSyncService } from '../../../core/offline/offline-sync.service';
import { PendingOperationsRepository } from '../../../core/offline/pending-operations.repository';
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
  [class.justify-start]="message.senderUserId !== currentUserId()">

  @if (isMessageSelectionMode()) {
  <button
    type="button"
    class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border text-xs"
    [class.border-blue-400]="isMessageSelected(message)"
    [class.bg-blue-500]="isMessageSelected(message)"
    [class.text-white]="isMessageSelected(message)"
    [class.border-slate-500]="!isMessageSelected(message)"
    [class.text-transparent]="!isMessageSelected(message)"
    (click)="toggleMessageSelection(message)"
  >
    ✓
  </button>
}

@if (message.senderUserId === currentUserId()) {
  <div class="relative shrink-0 opacity-0 transition-opacity group-hover:opacity-100">
    <button
      type="button"
      class="flex h-8 w-8 items-center justify-center rounded-full bg-slate-700/90 text-slate-200 shadow hover:bg-slate-600"
     (click)="toggleActionMenu($event, message)"
    >
      ⋯
    </button>


  </div>
}
 
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
                    {{ 'conversations.messages.deleted' | translate }}
                  </div>
                } @else {

  @if (message.forwardedFromMessageId) {
    <div
      class="mb-2 text-xs font-semibold text-blue-300"
      [class.px-2]="isMediaOnlyMessage(message)"
      [class.pt-2]="isMediaOnlyMessage(message)"
    >
      {{ 'conversations.messages.forward.forwardedFrom' | translate }}
      {{ message.forwardedFromDisplayName || 'Unknown' }}
    </div>
  }


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
@if (message.editedAtUtc) {
  <span class="ms-1">
    {{ 'conversations.messages.edited' | translate }}
  </span>
}
        @if (message.senderUserId === currentUserId()) {
          <span
            class="font-bold tracking-[-0.18em]"
           [class.text-sky-300]="getReceiptStatus(message) === 'seen'"
          >
            {{ getReceiptIcon(message) }}
          </span>
        }

@if (message.receiptStatus === 'failed') {
  <button
    type="button"
    class="ms-2 rounded-full bg-red-500/20 px-2 py-1 text-[11px] font-semibold text-red-200 hover:bg-red-500/30"
    (click)="messagesState.retryMessage(message)"
  >
    {{ 'conversations.messages.retry' | translate }}
  </button>
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
    {{ 'conversations.messages.replyingTo' | translate }}
  </div>

  @if (repliedMessage.deletedAtUtc) {

    <div class="mt-1 italic text-slate-400">
      {{ 'conversations.messages.deleted' | translate }}
    </div>

  } @else {

    <div class="mt-1 line-clamp-2 text-slate-200">
      {{
        repliedMessage.body
          ? repliedMessage.body
          : ((repliedMessage.attachments ?? []).length> 0
              ? '📎 Attachment'
              : '')
      }}
    </div>

  }
}
    @else {
      <div class="font-semibold text-blue-300">
        {{ 'conversations.messages.replyingToMessage' | translate }}
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
@if (message.editedAtUtc) {
  <span class="ms-1">
    {{ 'conversations.messages.edited' | translate }}
  </span>
}
          @if (message.senderUserId === currentUserId()) {
            <span
              class="font-bold tracking-[-0.18em]"
             [class.text-sky-300]="getReceiptStatus(message) === 'seen'"
            >
              {{ getReceiptIcon(message) }}
            </span>
          }
        </span>

@if (message.receiptStatus === 'failed') {
  <button
    type="button"
    class="ms-2 rounded-full bg-red-500/20 px-2 py-1 text-[11px] font-semibold text-red-200 hover:bg-red-500/30"
    (click)="messagesState.retryMessage(message)"
  >
    {{ 'conversations.messages.retry' | translate }}
  </button>
}

      }
@if (message.replyToMessageId) {
    <button
    type="button"
    class="mb-2 w-full rounded-lg border-s-4 border-blue-400 bg-black/20 px-3 py-2 text-start text-xs hover:bg-black/30"
    (click)="jumpToReplyMessage(message)"
  >
@if (findReplyMessage(message.replyToMessageId); as repliedMessage) {

  <div class="font-semibold text-blue-300">
    {{ 'conversations.messages.replyingTo' | translate }}
  </div>

  @if (repliedMessage.deletedAtUtc) {

    <div class="mt-1 italic text-slate-400">
      {{ 'conversations.messages.deleted' | translate }}
    </div>

  } @else {

    <div class="mt-1 line-clamp-2 text-slate-200">
      {{
        repliedMessage.body
          ? repliedMessage.body
          : ((repliedMessage.attachments ?? []).length > 0
              ? '📎 Attachment'
              : '')
      }}
    </div>

  }
}
    
    
    @else {
      <div class="font-semibold text-blue-300">
        {{ 'conversations.messages.replyingToMessage' | translate }}
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
@if ((message.attachments ?? []).length > 0) {
  <div
    class="relative overflow-hidden rounded-xl"
    [class.mt-2]="!isImageOnlyMessage(message)"
    [class.grid]="isImageAlbumMessage(message)"
    [class.grid-cols-2]="isImageAlbumMessage(message)"
    [class.gap-1]="isImageAlbumMessage(message)"
    [class.max-w-sm]="isImageAlbumMessage(message)"
  >
  @for (attachment of (message.attachments ?? []); track attachment.id) {
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

@if (message.receiptStatus === 'failed') {
  <button
    type="button"
    class="ms-2 rounded-full bg-red-500/80 px-2 py-1 text-[11px] font-semibold text-white"
    (click)="messagesState.retryMessage(message)"
  >
    Retry
  </button>
}

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
@if (message.receiptStatus === 'failed') {
  <button
    type="button"
    class="ms-2 rounded-full bg-red-500/20 px-2 py-1 text-[11px] font-semibold text-red-200 hover:bg-red-500/30"
    (click)="messagesState.retryMessage(message)"
  >
    {{ 'conversations.messages.retry' | translate }}
  </button>
}
    <span>
      {{ message.createdAtUtc | date:'shortTime' }}
    </span>
    @if (message.editedAtUtc) {
  <span class="ms-1">
    {{ 'conversations.messages.edited' | translate }}
  </span>
}
  </div>
}
              </div>


  
  @if (message.senderUserId !== currentUserId()) {
  <div class="relative shrink-0 opacity-0 transition-opacity group-hover:opacity-100">
    <button
      type="button"
      class="flex h-8 w-8 items-center justify-center rounded-full bg-slate-700/90 text-slate-200 shadow hover:bg-slate-600"
      (click)="toggleActionMenu($event, message)"
    >
      ⋯
    </button>

 
  </div>
}

            </div>
          }
        </div>
      </section>


@if (isMessageSelectionMode()) {
  <div class="border-t border-white/10 bg-slate-900 px-4 py-3">
    <div class="mx-auto flex w-full max-w-4xl items-center justify-between gap-3">
      <button
        type="button"
        class="rounded-xl px-4 py-2 text-sm text-slate-300 hover:bg-white/10"
        (click)="cancelMessageSelection()"
      >
        {{ 'common.cancel' | translate }}
      </button>

      <div class="text-sm text-slate-300">
        {{ selectedMessageIds().length }}
        {{ 'conversations.messages.selection.selected' | translate }}
      </div>

      <button
        type="button"
        class="rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-500"
        (click)="startForwardSelectedMessages()"
      >
        {{ 'conversations.messages.actions.forward' | translate }}
      </button>
    </div>
  </div>
}




 @if (isForwardModalOpen()) {
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
    <div class="w-full max-w-md overflow-hidden rounded-2xl bg-slate-900 text-white shadow-2xl">
      <div class="flex items-center justify-between border-b border-white/10 px-5 py-4">
        <div>
          <h3 class="text-base font-bold">
            {{ 'conversations.messages.forward.title' | translate }}
          </h3>

          <p class="mt-1 text-xs text-slate-400">
            {{ forwardingMessage()?.body || '📎 Attachment' }}
          </p>
        </div>

        <button
          type="button"
          class="text-slate-400 hover:text-white"
          (click)="cancelForwardMessage()"
        >
          ×
        </button>
      </div>

      <div class="max-h-80 overflow-y-auto p-3">
        @for (conversation of conversationsState.conversations(); track conversation.id) {
          @if (conversation.id !== conversationId()) {
            <button
              type="button"
              class="flex w-full items-center gap-3 rounded-xl px-3 py-3 text-start hover:bg-white/10"
              (click)="toggleForwardTarget(conversation.id)"
            >
              <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-600 text-sm font-bold text-white">
                {{ conversation.displayTitle.charAt(0) }}
              </div>

              <div class="min-w-0 flex-1">
                <div class="truncate text-sm font-semibold">
                  {{ conversation.displayTitle }}
                </div>

                <div class="truncate text-xs text-slate-400">
                  {{ conversation.lastMessagePreview || ('conversations.list.noMessages' | translate) }}
                </div>
              </div>

              @if (isForwardTargetSelected(conversation.id)) {
                <div class="flex h-6 w-6 items-center justify-center rounded-full bg-blue-500 text-xs font-bold">
                  ✓
                </div>
              } @else {
                <div class="h-6 w-6 rounded-full border border-slate-500"></div>
              }
            </button>
          }
        }
      </div>

      <div class="flex items-center justify-end gap-3 border-t border-white/10 px-5 py-4">
        <button
          type="button"
          class="rounded-xl px-4 py-2 text-sm text-slate-300 hover:bg-white/10"
          (click)="cancelForwardMessage()"
        >
          {{ 'common.cancel' | translate }}
        </button>

<button
  type="button"
  class="rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-500 disabled:opacity-50"
  [disabled]="selectedForwardConversationIds().length === 0 || isForwarding()"
  (click)="forwardSelectedMessage()"
>
  @if (isForwarding()) {
    {{ 'common.sending' | translate }}
  } @else {
    {{ 'conversations.messages.forward.send' | translate }}
  }
</button>
      </div>
    </div>
  </div>
}
@if (actionMenuMessage(); as actionMessage) {
  @if (actionMenuPosition(); as pos) {
    <button
      type="button"
      class="fixed inset-0 z-40 cursor-default bg-transparent"
      (click)="closeActionMenu()"
      aria-label="Close menu"
    ></button>

    <div
      class="fixed z-50 min-w-40 overflow-hidden rounded-xl bg-slate-800 py-1 text-sm text-slate-100 shadow-2xl"
      [style.left.px]="pos.x"
      [style.top.px]="pos.y"
    >
      <button
        type="button"
        class="block w-full px-4 py-2 text-start hover:bg-white/10"
        (click)="replyToMessage(actionMessage)"
      >
        {{ 'conversations.messages.actions.reply' | translate }}
      </button>

      <button
  type="button"
  class="block w-full px-4 py-2 text-start hover:bg-white/10"
  (click)="startForwardMessage(actionMessage)"
>
  {{ 'conversations.messages.actions.forward' | translate }}
</button>


<button
  type="button"
  class="block w-full px-4 py-2 text-start hover:bg-white/10"
  (click)="startMessageSelection(actionMessage)"
>
  {{ 'conversations.messages.actions.select' | translate }}
</button>

      <button
        type="button"
        class="block w-full px-4 py-2 text-start hover:bg-white/10"
        (click)="copyMessage(actionMessage)"
        [disabled]="!actionMessage.body || actionMessage.body.trim().length === 0"
        [class.opacity-50]="!actionMessage.body || actionMessage.body.trim().length === 0"
      >
        {{ 'conversations.messages.actions.copy' | translate }}
      </button>

      @if (actionMessage.senderUserId === currentUserId()) {
        <button
          type="button"
          class="block w-full px-4 py-2 text-start hover:bg-white/10"
          (click)="startEditMessage(actionMessage)"
        >
          {{ 'conversations.messages.actions.edit' | translate }}
        </button>

        <button
          type="button"
          class="block w-full px-4 py-2 text-start text-red-300 hover:bg-red-500/10"
          (click)="deleteMessage(actionMessage)"
        >
          {{ 'conversations.messages.actions.delete' | translate }}
        </button>
      }
    </div>
  }
}

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
   
@if (replyingToMessage()) {
  <div class="mb-3 w-full overflow-hidden rounded-xl border-s-4 border-blue-500 bg-slate-800 px-4 py-3">
    <div class="flex min-w-0 items-start justify-between gap-3">
      <div class="min-w-0 flex-1">
        <div class="text-xs font-semibold text-blue-400">
          {{ 'conversations.messages.replyingTo' | translate }}
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


@if (editingMessage()) {
  <div class="mb-3 w-full overflow-hidden rounded-xl border-s-4 border-amber-500 bg-slate-800 px-4 py-3">
    <div class="flex min-w-0 items-start justify-between gap-3">
      <div class="min-w-0 flex-1">
        <div class="text-xs font-semibold text-amber-400">
          {{ 'conversations.messages.editing' | translate }}
        </div>

        <div
          class="mt-1 max-h-10 overflow-hidden break-words text-sm leading-5 text-slate-300"
          dir="auto"
        >
          {{ editingMessage()?.body }}
        </div>
      </div>

      <button
        type="button"
        class="shrink-0 text-slate-400 hover:text-white"
        (click)="cancelEditMessage()"
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
  class="rounded-2xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-500">
 @if (editingMessage()) {
  {{ 'conversations.messages.input.save' | translate }}
} @else {
  {{ 'conversations.messages.input.send' | translate }}
}
</button>

 
  </form>
</footer>
 
    </div>
  `
})
export class ConversationChatComponent implements OnInit, OnDestroy {
  private readonly pendingOperations = inject(PendingOperationsRepository);
 
  readonly editingMessage = signal<MessageDto | null>(null);
  readonly isMessageSelectionMode = signal(false);
  readonly selectedMessageIds = signal<string[]>([]);
  private readonly router = inject(Router);
  private readonly messagesApi = inject(MessagesApiService);
  readonly offlineSync = inject(OfflineSyncService);
  readonly isForwarding = signal(false);
  readonly selectedForwardConversationIds = signal<string[]>([]);
  readonly actionMenuPosition = signal<{ x: number; y: number } | null>(null);
  readonly actionMenuMessage = signal<MessageDto | null>(null);
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

  readonly conversationsState = inject(ConversationsState);
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
  readonly forwardingMessage = signal<MessageDto | null>(null);
  readonly isForwardModalOpen = signal(false);



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
       
        this.presenceState.setSnapshot(
          event.conversationId,
          event.userIds
        );



      });

    this.chatSignalR.presenceChanged$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
       


        this.presenceState.setUserPresence(
          event.conversationId,
          event.userId,
          event.isOnline
        );
      });



    this.offlineSync.messageSyncFailed$
      .pipe(takeUntil(this.destroy$))
      .subscribe(clientMessageId => {
        this.messagesState.markMessageAsFailed(clientMessageId);
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



    this.offlineSync.messageSynced$
      .pipe(takeUntil(this.destroy$))
      .subscribe(message => {
        if (message.conversationId === this.conversationId()) {
          this.messagesState.handleSyncedMessage(message);
        }
      });


    this.chatSignalR.reconnected$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        const conversationId = this.conversationId();
this.offlineSync.requestSync('signalr-reconnected');
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



syncNow(): void {
  alert('sync clicked');
 
  void this.offlineSync.forceSyncPendingOperations();
}


  startForwardMessage(message: MessageDto): void {
    this.forwardingMessage.set(message);
    this.isForwardModalOpen.set(true);
    this.closeActionMenu();
  }

  cancelForwardMessage(): void {
    this.forwardingMessage.set(null);
    this.isForwardModalOpen.set(false);
    this.selectedForwardConversationIds.set([]);
  }

  private readonly handleVisibilityChange = (): void => {
    if (document.visibilityState === 'visible') {
      this.markCurrentConversationAsSeenIfAllowed();
    }
  };

  toggleActionMenu(event: MouseEvent, message: MessageDto): void {
    event.stopPropagation();

    if (this.openActionMenuMessageId() === message.id) {
      this.closeActionMenu();
      return;
    }

    this.openActionMenuMessageId.set(message.id);
    this.actionMenuMessage.set(message);

    const menuWidth = 160;
    const menuHeight = message.senderUserId === this.currentUserId() ? 180 : 100;

    let x = event.clientX;
    let y = event.clientY;

    if (x + menuWidth > window.innerWidth) {
      x = window.innerWidth - menuWidth - 12;
    }

    if (y + menuHeight > window.innerHeight) {
      y = window.innerHeight - menuHeight - 12;
    }

    this.actionMenuPosition.set({ x, y });
  }

  closeActionMenu(): void {
    this.openActionMenuMessageId.set(null);
    this.actionMenuMessage.set(null);
    this.actionMenuPosition.set(null);
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



private stopTypingState(conversationId: string | null): void {
  if (conversationId && this.isTypingSent) {
    void this.chatSignalR.stopTyping(conversationId);
    this.isTypingSent = false;
  }

  if (this.typingStopTimer) {
    clearTimeout(this.typingStopTimer);
    this.typingStopTimer = null;
  }
}


sendMessage(): void {
  const conversationId = this.conversationId();
  const rawBody = this.messageText().trim();

  const editingMessage = this.editingMessage();

  if (editingMessage) {
    if (!rawBody) {
      return;
    }

    const editedAtUtc = new Date().toISOString();

    this.messagesState.handleMessageEdited({
      id: editingMessage.id,
      body: rawBody,
      editedAtUtc
    });

    this.editingMessage.set(null);
    this.messageText.set('');

    if (!navigator.onLine) {
      void this.pendingOperations.add('editMessage', {
        messageId: editingMessage.id,
        body: rawBody
      });

      this.stopTypingState(conversationId);
      return;
    }

    this.messagesApi.editMessage(editingMessage.id, {
      body: rawBody
    }).subscribe();

    this.stopTypingState(conversationId);
    return;
  }

  if (!conversationId || !rawBody) {
    return;
  }

  this.messagesState.send({
    conversationId,
    clientMessageId: crypto.randomUUID(),
    body: rawBody,
    replyToMessageId: this.replyingToMessage()?.id ?? null,
    attachments: []
  });

  this.messageText.set('');
  this.replyingToMessage.set(null);
  this.selectedAttachments.set([]);

  this.stopTypingState(conversationId);
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
     

      await this.chatSignalR.leaveConversation(oldConversationId);
      this.presenceState.clearConversation(oldConversationId);
    }

    this.activeConversationId = newConversationId;
    this.conversationId.set(newConversationId);
    this.messagesState.clear();
this.selectedAttachments.set([]);
this.pendingImageFile.set(null);
this.isSendModeOpen.set(false);
this.replyingToMessage.set(null);
this.editingMessage.set(null);
    this.lastDeliveredSequenceNumber = null;
    this.lastSeenSequenceNumber = null;

    if (!newConversationId) {
      return;
    }

    this.messagesState.load(newConversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
         

          this.scrollToBottom();

          requestAnimationFrame(() => {
            this.markCurrentConversationAsDelivered();
            this.markCurrentConversationAsSeenIfAllowed();
          });
        },
        error: error => {
          console.error('messagesState.load failed:', error);
        }
      });

    await this.chatSignalR.joinConversation(newConversationId);

    this.chatSignalR.startPresenceRenewTimer(newConversationId);
  }

  onFileSelected(event: Event): void {
  const input = event.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);

  input.value = '';

  const conversationId = this.conversationId();

  if (!conversationId || files.length === 0) {
    return;
  }

  files.forEach(file => {
    if (file.type.startsWith('image/')) {
      void this.sendImageAsLocalMediaMessage(file);
      return;
    }

    void this.messagesState.sendLocalMediaMessage({
      conversationId,
      file,
      body: '',
      replyToMessageId: this.replyingToMessage()?.id ?? null
    });
  });

  this.replyingToMessage.set(null);
  this.scrollToBottom();
}


private async sendImageAsLocalMediaMessage(file: File): Promise<void> {
  const conversationId = this.conversationId();

  if (!conversationId) {
    return;
  }

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

    await this.messagesState.sendLocalMediaMessage({
      conversationId,
      file: renamedFile,
      body: '',
      replyToMessageId: this.replyingToMessage()?.id ?? null
    });
  } catch {
    await this.messagesState.sendLocalMediaMessage({
      conversationId,
      file,
      body: '',
      replyToMessageId: this.replyingToMessage()?.id ?? null
    });
  }
}

  // private async compressAndUploadImage(file: File): Promise<void> {
  //   const tempId = this.addPendingAttachment(file);

  //   try {

  //     const compressedFile = await imageCompression(file, {
  //       maxSizeMB: 1,
  //       maxWidthOrHeight: 1600,
  //       useWebWorker: true,
  //       fileType: 'image/jpeg',
  //       initialQuality: 0.8
  //     });

  //     const renamedFile = new File(
  //       [compressedFile],
  //       file.name,
  //       {
  //         type: compressedFile.type,
  //         lastModified: Date.now()
  //       }
  //     );
  //     this.updateSelectedAttachment(tempId, {
  //       localFile: renamedFile,
  //       isUploading: navigator.onLine,
  //       isOfflinePending: !navigator.onLine,
  //       progress: navigator.onLine ? 0 : 100
  //     });

  //     if (!navigator.onLine) {
  //       return;
  //     }

  //     this.uploadAttachmentByTempId(tempId, renamedFile);
  //   } catch {
  //     this.updateSelectedAttachment(tempId, {
  //       isUploading: false,
  //       error: 'Compression failed'
  //     });
  //   }
  // }


  sendPendingImageAsFile(): void {
    const file = this.pendingImageFile();

    if (!file) {
      return;
    }

    this.isSendModeOpen.set(false);
    this.pendingImageFile.set(null);

   // this.uploadAttachment(file);
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

   // this.uploadAttachment(compressedFile);
  }

  cancelSendMode(): void {
    this.isSendModeOpen.set(false);
    this.pendingImageFile.set(null);
  }


  // private uploadAttachment(file: File): void {
  //   const tempId = this.addPendingAttachment(file);

  //   if (!navigator.onLine) {
  //     return;
  //   }

  //   this.uploadAttachmentByTempId(tempId, file);
  // }



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
      (message.attachments ?? []).length > 0 &&
      message.attachments.every(x => x.contentType.startsWith('image/')) &&
      (!message.body || message.body.trim().length === 0)
    );
  }
  // private addPendingAttachment(file: File): string {
  //   const tempId = crypto.randomUUID();

  //   const localPreviewUrl = file.type.startsWith('image/')
  //     ? URL.createObjectURL(file)
  //     : undefined;

  //   this.selectedAttachments.set([
  //     ...this.selectedAttachments(),
  //     {
  //       fileId: tempId,
  //       fileName: file.name,
  //       contentType: file.type || 'application/octet-stream',
  //       size: file.size,
  //       storagePath: '',
  //       progress: 0,
  //       isUploading: navigator.onLine,
  //       error: null,
  //       localPreviewUrl,
  //       localFile: file,
  //       isOfflinePending: !navigator.onLine,
  //     }
  //   ]);

  //   return tempId;
  // }

  // private uploadAttachmentByTempId(tempId: string, file: File): void {
  //   this.filesApi.uploadWithProgress(file).subscribe({
  //     next: event => {
  //       if (event.type === HttpEventType.UploadProgress) {
  //         const progress = event.total
  //           ? Math.round((event.loaded / event.total) * 100)
  //           : 0;

  //         this.updateSelectedAttachment(tempId, { progress });
  //       }

  //       if (event.type === HttpEventType.Response && event.body) {
  //         this.updateSelectedAttachment(tempId, {
  //           fileId: event.body.fileId,
  //           fileName: event.body.fileName,
  //           contentType: event.body.contentType,
  //           size: event.body.size,
  //           storagePath: event.body.storagePath,
  //           progress: 100,
  //           isUploading: false,
  //           error: null
  //         });
  //       }
  //     },
  //     error: () => {
  //       this.updateSelectedAttachment(tempId, {
  //         isUploading: false,
  //         error: 'Upload failed'
  //       });
  //     }
  //   });
  // }
  isImageAlbumMessage(message: MessageDto): boolean {
    return (
      (message.attachments ?? []).length > 1 &&
      message.attachments.every(x => x.contentType.startsWith('image/'))
    );
  }
  isMediaOnlyMessage(message: MessageDto): boolean {
    return (
      (message.attachments ?? []).length > 0 &&
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
    if (!navigator.onLine) {
      return;
    }
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
    if (!navigator.onLine) {
      return;
    }
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
  isMyMessage(message: MessageDto): boolean {
    return message.senderUserId === this.currentUserId();
  }

  startEditMessage(message: MessageDto): void {
    if (!message.body || message.body.trim().length === 0) {
      return;
    }

    this.editingMessage.set(message);
    this.messageText.set(message.body);
    this.closeActionMenu();
  }

  cancelEditMessage(): void {
    this.editingMessage.set(null);
    this.messageText.set('');
  }

deleteMessage(message: MessageDto): void {
  this.closeActionMenu();

  const isPendingMessage =
     
    message.receiptStatus === 'sending' ||
    message.sequenceNumber < 0;

  if (isPendingMessage) {
    this.messagesState.deletePendingLocally(message);
    return;
  }

  if (!navigator.onLine) {
    this.messagesState.deleteLocally(message);
    return;
  }

  this.messagesApi.deleteMessage(message.id).subscribe({
    next: () => {},
    error: () => {
      this.messagesState.deleteLocally(message);
    }
  });
}
  copyMessage(message: MessageDto): void {
    if (!message.body) {
      return;
    }
  }
  shouldOpenMenuUp(messageId: string): boolean {
    const element = document.getElementById(`message-${messageId}`);

    if (!element) {
      return false;
    }

    const rect = element.getBoundingClientRect();
    const viewportHeight = window.innerHeight;

    return viewportHeight - rect.bottom < 180;
  }
  toggleForwardTarget(conversationId: string): void {
    const current = this.selectedForwardConversationIds();

    if (current.includes(conversationId)) {
      this.selectedForwardConversationIds.set(
        current.filter(x => x !== conversationId)
      );
      return;
    }

    this.selectedForwardConversationIds.set([
      ...current,
      conversationId
    ]);
  }

  isForwardTargetSelected(conversationId: string): boolean {
    return this.selectedForwardConversationIds().includes(conversationId);
  }


  forwardSelectedMessage(): void {
    const targetConversationIds = this.selectedForwardConversationIds();

    if (targetConversationIds.length === 0) {
      return;
    }

    this.isForwarding.set(true);

    if (this.isMessageSelectionMode()) {
      const sourceMessageIds = this.selectedMessageIds();

      if (sourceMessageIds.length === 0) {
        this.isForwarding.set(false);
        return;
      }

      this.messagesApi.forwardMessagesBatch({
        sourceMessageIds,
        targetConversationIds,
        comment: null
      }).subscribe({
        next: () => {
          const targetConversationId = targetConversationIds[0];

          this.isForwarding.set(false);
          this.cancelForwardMessage();
          this.cancelMessageSelection();

          this.router.navigate([
            '/app/conversations',
            targetConversationId
          ]);
        },
        error: () => {
          this.isForwarding.set(false);
        }
      });

      return;
    }

    const message = this.forwardingMessage();

    if (!message) {
      this.isForwarding.set(false);
      return;
    }

    this.messagesApi.forwardMessage({
      sourceMessageId: message.id,
      targetConversationIds,
      comment: null
    }).subscribe({
      next: () => {
        const targetConversationId = targetConversationIds[0];

        this.isForwarding.set(false);
        this.cancelForwardMessage();

        this.router.navigate([
          '/app/conversations',
          targetConversationId
        ]);
      },
      error: () => {
        this.isForwarding.set(false);
      }
    });
  }



  startMessageSelection(message: MessageDto): void {
    this.isMessageSelectionMode.set(true);
    this.selectedMessageIds.set([message.id]);
    this.closeActionMenu();
  }

  cancelMessageSelection(): void {
    this.isMessageSelectionMode.set(false);
    this.selectedMessageIds.set([]);
  }

  toggleMessageSelection(message: MessageDto): void {
    const current = this.selectedMessageIds();

    if (current.includes(message.id)) {
      const next = current.filter(x => x !== message.id);

      this.selectedMessageIds.set(next);

      if (next.length === 0) {
        this.isMessageSelectionMode.set(false);
      }

      return;
    }

    this.selectedMessageIds.set([...current, message.id]);
  }

  isMessageSelected(message: MessageDto): boolean {
    return this.selectedMessageIds().includes(message.id);
  }

  startForwardSelectedMessages(): void {
    if (this.selectedMessageIds().length === 0) {
      return;
    }

    this.forwardingMessage.set(null);
    this.isForwardModalOpen.set(true);
    this.closeActionMenu();
  }



}