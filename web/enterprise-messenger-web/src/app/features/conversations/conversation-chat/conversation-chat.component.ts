import { DisplayLatencyService } from '../../../core/telemetry/display-latency.service';
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
import { MessageComposerComponent } from '../message-composer/message-composer';
import { MessageBubbleComponent } from '../message-bubble/message-bubble';
import { MessageActionMenuComponent } from '../message-action-menu/message-action-menu';
import { MessageForwardModalComponent } from '../message-forward-modal/message-forward-modal';
@Component({
  selector: 'app-conversation-chat',
  standalone: true,
  imports: [
    DatePipe,
    TranslateModule,
    FormsModule,
    AttachmentPreviewComponent,
    MessageComposerComponent,
    MessageBubbleComponent,
    MessageActionMenuComponent,
    MessageForwardModalComponent 
  ] ,
  templateUrl: './conversation-chat.component.html'
 
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
    readonly authService = inject(AuthService);
  private activeConversationId: string | null = null;

  readonly conversationsState = inject(ConversationsState);
  private readonly chatSignalR = inject(ChatSignalRService);
  private readonly displayLatency = inject(DisplayLatencyService);
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

    window.addEventListener('online', this.onlineHandler);

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


    this.offlineSync.messageEditSynced$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
       


        this.messagesState.handleMessageEdited(
          {
            id: event.messageId,
            body: event.body,
            editedAtUtc: event.editedAtUtc,
            editSyncStatus: 'idle'
          });
      });

    this.offlineSync.messageDeleteSynced$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.messagesState.markMessageDeletedSynced(
          event.messageId,
          event.deletedAtUtc
        );
      });

    this.offlineSync.messageDeleteSyncFailed$
      .pipe(takeUntil(this.destroy$))
      .subscribe(messageId => {
       
        this.messagesState.markMessageDeleteFailed(messageId);
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

          if (event.senderUserId !== this.currentUserId() && !event.isDuplicate) {
            this.displayLatency.recordDisplayed(event.createdAtUtc);
          }

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

    this.offlineSync.messageEditSyncFailed$
      .pipe(takeUntil(this.destroy$))
      .subscribe(messageId => {
        this.messagesState.markMessageEditAsFailed(messageId);
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
        editedAtUtc,
        editSyncStatus: 'syncing'
      });

      this.editingMessage.set(null);
      this.messageText.set('');

if (!navigator.onLine) {
  void this.pendingOperations.upsertByMessageId(
    'editMessage',
    editingMessage.id,
    {
      messageId: editingMessage.id,
      body: rawBody,
      editedAtUtc
    }
  );

  this.stopTypingState(conversationId);
  return;
}

      this.messagesApi.editMessage(editingMessage.id, {
        body: rawBody
      }).subscribe({
        next: () => {
          this.messagesState.handleMessageEdited({
            id: editingMessage.id,
            body: rawBody,
            editedAtUtc,
            editSyncStatus: 'idle'
          });
        },
        error: () => {


          void this.pendingOperations.upsertByMessageId(
            'editMessage',
            editingMessage.id,
            {
              messageId: editingMessage.id,
              body: rawBody,
              editedAtUtc
            }
          );

          this.offlineSync.requestSync('edit-message-failed-queued');
        }
      });

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
    window.removeEventListener('online', this.onlineHandler);
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

  private readonly onlineHandler = (): void => {
   

    void this.chatSignalR.reconnectAndSync('browser-online');
  };

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



  retryMessage(message: MessageDto, event?: Event): void {
    event?.stopPropagation();

 

    this.messagesState.retryMessage(message);
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
      message.sequenceNumber < 0 ||
      (message.attachments ?? []).some(x => x.isLocalPending);

    if (isPendingMessage) {
      this.messagesState.deletePendingLocally(message);
      return;
    }

    this.messagesState.markMessageDeleteSyncing(message.id);

    void this.pendingOperations.upsertByMessageId(
      'deleteMessage',
      message.id,
      {
        messageId: message.id,
        deletedAtUtc: new Date().toISOString()
      }
    );

    this.offlineSync.requestSync('delete-message-queued');
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