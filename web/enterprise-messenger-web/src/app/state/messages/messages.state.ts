import { Injectable, computed, inject, signal } from '@angular/core';
import { SendMessageRequest } from '../../features/messages/models/send-message.request';
import { MessagesApiService } from '../../core/services/messages-api.service';
import { MessageDto } from '../../features/messages/models/message.dto';
import {
  MessageCreatedEvent,
  MessageDeletedEvent,
  MessageEditedEvent
} from '../../core/signalr/chat-signalr.models';
import { PendingOperationsRepository } from '../../core/offline/pending-operations.repository';
import { OfflineMessagesRepository } from '../../core/offline/offline-messages.repository';
import { OfflineConversationsRepository } from '../../core/offline/offline-conversations.repository';
import { AuthService } from '../../core/auth/auth.service';
import { Observable, catchError, finalize, firstValueFrom, from, map, of, switchMap, tap } from 'rxjs';
import { ConversationsState } from '../conversations/conversations.state';
import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';
import { OfflineSyncService } from '../../core/offline/offline-sync.service';
import { OfflineFilesRepository } from '../../core/offline/offline-files.repository';
import { FilesApiService } from '../../core/services/files-api.service';
@Injectable({
  providedIn: 'root'
})
export class MessagesState {

  private readonly offlineFiles = inject(OfflineFilesRepository);
  private readonly conversationsSignal = signal<ConversationListItemDto[]>([]);
  private readonly offlineConversations = inject(OfflineConversationsRepository);
  private readonly conversationsState = inject(ConversationsState);
  private readonly messagesApi = inject(MessagesApiService);
  private readonly offlineMessages = inject(OfflineMessagesRepository);
  private readonly filesApi = inject(FilesApiService);

  private readonly offlineSync = inject(OfflineSyncService);
  private readonly messagesSignal = signal<MessageDto[]>([]);
  private readonly isLoadingSignal = signal(false);

  private readonly pendingOperations = inject(PendingOperationsRepository);
  readonly messages = this.messagesSignal.asReadonly();
  readonly isLoading = this.isLoadingSignal.asReadonly();
  private readonly authService = inject(AuthService);
  readonly currentUserId = computed(() => {
    return this.authService.user()?.id;
  });

  handleSyncedMessage(message: MessageDto): void {
    const current = this.messagesSignal();

    const existingIndex = current.findIndex(x =>
      x.id === message.id ||
      (
        !!message.clientMessageId &&
        x.clientMessageId === message.clientMessageId
      )
    );

    const syncedMessage: MessageDto = {
      ...message,
      receiptStatus: 'sent'
    };

    if (existingIndex >= 0) {
      const updated = [...current];
      updated[existingIndex] = syncedMessage;
      this.messagesSignal.set(this.sortMessages(updated));
      return;
    }

    this.messagesSignal.set(
      this.sortMessages([...current, syncedMessage])
    );

    void this.offlineMessages.save(syncedMessage);

  }




  async sendLocalMediaMessage(input: {
    conversationId: string;
    file: File;
    body: string;
    replyToMessageId: string | null;
  }): Promise<void> {
    const currentUserId = this.authService.user()?.id;

    if (!currentUserId) {
      return;
    }

    const clientMessageId = crypto.randomUUID();
    const localAttachmentId = crypto.randomUUID();
    const createdAtUtc = new Date().toISOString();

    const pendingFile = await this.offlineFiles.save(input.file);

    const optimisticMessage: MessageDto = {
      id: clientMessageId,
      conversationId: input.conversationId,
      sequenceNumber: -1,
      senderUserId: currentUserId,
      body: input.body,
      replyToMessageId: input.replyToMessageId,
      replyToSequenceNumber: null,
      createdAtUtc,
      editedAtUtc: null,
      deletedAtUtc: null,
      clientMessageId,
      receiptStatus: 'sending',

      attachments: [
        {
          id: localAttachmentId,
          fileName: input.file.name || 'file',
          contentType: input.file.type || 'application/octet-stream',
          size: input.file.size,
          storagePath: '',
          localFileId: pendingFile.id,
          isLocalPending: true,
          uploadProgress: null
        }
      ]
    };

    this.messagesSignal.set(
      this.sortMessages([...this.messagesSignal(), optimisticMessage])
    );

    await this.offlineMessages.save(optimisticMessage, 'pending');

    this.conversationsState.updateLastMessageLocally({
      conversationId: input.conversationId,
      body: '📎 Attachment',
      senderUserId: currentUserId,
      senderDisplayName: this.authService.user()?.displayName,
      createdAtUtc
    });

    if (!navigator.onLine) {
await this.pendingOperations.upsertByClientMessageId(
  'sendMessageWithAttachments',
  clientMessageId,
  {
    conversationId: input.conversationId,
    clientMessageId,
    body: input.body,
    replyToMessageId: input.replyToMessageId,
    fileIds: [pendingFile.id]
  }
);

      return;
    }

    try {
      const uploaded = await firstValueFrom(
        this.filesApi.upload(input.file)
      );

      const sentMessage = await firstValueFrom(
        this.messagesApi.sendMessage({
          conversationId: input.conversationId,
          clientMessageId,
          body: input.body,
          replyToMessageId: input.replyToMessageId,
          attachments: [
            {
              fileId: uploaded.fileId,
              fileName: uploaded.fileName,
              contentType: uploaded.contentType,
              size: uploaded.size,
              storagePath: uploaded.storagePath
            }
          ]
        })
      );

      const syncedMessage: MessageDto = {
        ...sentMessage,
        clientMessageId,
        receiptStatus: 'sent',

      };

      this.handleSyncedMessage(syncedMessage);
      await this.offlineMessages.save(syncedMessage);
      await this.offlineFiles.remove(pendingFile.id);
    } catch {
await this.pendingOperations.upsertByClientMessageId(
  'sendMessageWithAttachments',
  clientMessageId,
  {
    conversationId: input.conversationId,
    clientMessageId,
    body: input.body,
    replyToMessageId: input.replyToMessageId,
    fileIds: [pendingFile.id]
  }
);
      this.offlineSync.requestSync('media-send-failed-queued');
    }
  }
  deletePendingLocally(message: MessageDto): void {
    this.messagesSignal.set(
      this.messagesSignal().filter(x => x.id !== message.id)
    );

    void this.offlineMessages.deleteById(message.id);

    if (message.clientMessageId) {
      void this.pendingOperations.deleteByClientMessageId(message.clientMessageId);
    }

    for (const attachment of message.attachments ?? []) {
      if (attachment.localFileId) {
        void this.offlineFiles.remove(attachment.localFileId);
      }
    }
  }


  deleteLocally(message: MessageDto, deletedAtUtc = new Date().toISOString()): void {
    let updatedMessage: MessageDto | null = null;

    this.messagesSignal.set(
      this.messagesSignal().map(x => {
        if (x.id !== message.id) {
          return x;
        }

        updatedMessage = {
          ...x,
          body: '',
          attachments: [],
          deletedAtUtc
        };

        return updatedMessage;
      })
    );

    if (updatedMessage) {
      void this.offlineMessages.save(updatedMessage, 'pending');
    }
  }

  markMessageDeleteSyncing(messageId: string): void {
    this.messagesSignal.set(
      this.messagesSignal().map(message =>
        message.id === messageId
          ? {
            ...message,
            deleteSyncStatus: 'syncing'
          }
          : message
      )
    );

    const message = this.messagesSignal().find(x => x.id === messageId);

    if (message) {
      void this.offlineMessages.save(message, 'pending');
    }
  }

  markMessageDeleteFailed(messageId: string): void {
    this.messagesSignal.set(
      this.messagesSignal().map(message =>
        message.id === messageId
          ? {
            ...message,
            deleteSyncStatus: 'failed'
          }
          : message
      )
    );

    const message = this.messagesSignal().find(x => x.id === messageId);

    if (message) {
      void this.offlineMessages.save(message, 'pending');
    }
  }

  markMessageDeletedSynced(messageId: string, deletedAtUtc: string): void {
    this.messagesSignal.set(
      this.messagesSignal().map(message =>
        message.id === messageId
          ? {
            ...message,
            body: '',
            attachments: [],
            deletedAtUtc,
            deleteSyncStatus: 'idle'
          }
          : message
      )
    );

    const message = this.messagesSignal().find(x => x.id === messageId);

    if (message) {
      void this.offlineMessages.save(message);
    }
  }

  load(conversationId: string): Observable<void> {
    this.isLoadingSignal.set(true);

    return from(this.offlineMessages.getByConversationId(conversationId)).pipe(
      tap(cachedMessages => {
        const normalizedCachedMessages = cachedMessages.map(message =>
          this.normalizeMessage(message)
        );

        this.messagesSignal.set(
          this.sortMessages(normalizedCachedMessages)
        );
      }),

      switchMap(() => {
        if (!navigator.onLine) {
          return of(null);
        }

        return this.messagesApi
          .getConversationMessages(conversationId, 50)
          .pipe(
            catchError(error => {
              
              return of(null);
            })
          );
      }),

      tap(response => {
        if (!response) {
          return;
        }

        const serverMessages = response.map(message =>
          this.normalizeMessage(message)
        );

        const localMessages = this.messagesSignal()
          .filter(message => message.conversationId === conversationId)
          .map(message => this.normalizeMessage(message));

        const localPendingMessages = localMessages.filter(message =>
          this.isLocalPendingMessage(message)
        );

        const merged = this.sortMessages([
          ...serverMessages,
          ...localPendingMessages.filter(local =>
            !serverMessages.some(server =>
              this.isSameMessage(server, local)
            )
          )
        ]);

        this.messagesSignal.set(merged);

        void this.offlineMessages.saveMany(serverMessages);

        for (const pendingMessage of localPendingMessages) {
          const existsOnServer = serverMessages.some(server =>
            this.isSameMessage(server, pendingMessage)
          );

          if (existsOnServer) {
            if (pendingMessage.clientMessageId) {
              void this.offlineMessages.deleteByClientMessageId(
                pendingMessage.clientMessageId
              );
            } else {
              void this.offlineMessages.deleteById(pendingMessage.id);
            }

            continue;
          }

          void this.offlineMessages.save(pendingMessage, 'pending');
        }
      }),

      finalize(() => {
        this.isLoadingSignal.set(false);
      }),

      map(() => void 0)
    );
  }


  clear(): void {
    this.messagesSignal.set([]);
  }

  send(request: SendMessageRequest): void {
    const currentUserId = this.authService.user()?.id;

    if (!currentUserId) {
      return;
    }

    const optimisticMessage: MessageDto = {
      id: request.clientMessageId,
      conversationId: request.conversationId,
      sequenceNumber: -1,
      senderUserId: currentUserId,
      body: request.body,
      replyToMessageId: request.replyToMessageId,
      replyToSequenceNumber: null,
      createdAtUtc: new Date().toISOString(),
      editedAtUtc: null,
      deletedAtUtc: null,
      attachments: [],
      clientMessageId: request.clientMessageId,
      receiptStatus: 'sending',

    };


    this.conversationsState.updateLastMessageLocally({
      conversationId: request.conversationId,
      body: request.body,
      senderUserId: currentUserId,
      senderDisplayName: this.authService.user()?.displayName,
      createdAtUtc: optimisticMessage.createdAtUtc
    });


    this.messagesSignal.set(
      this.sortMessages([
        ...this.messagesSignal(),
        optimisticMessage
      ])
    );

    void this.offlineMessages.save(optimisticMessage, 'pending');

if (!navigator.onLine) {
  void this.pendingOperations.upsertByClientMessageId(
    'sendMessage',
    request.clientMessageId,
    request
  );

  return;
}

    this.messagesApi.sendMessage(request).subscribe({
      next: message => {
        const sentMessage: MessageDto = {
          ...message,
          clientMessageId: request.clientMessageId,
          receiptStatus: 'sent',

        };

        this.messagesSignal.set(
          this.sortMessages(
            this.messagesSignal().map(x =>
              x.clientMessageId === request.clientMessageId
                ? sentMessage
                : x
            )
          )
        );

        void this.offlineMessages.save(sentMessage);
      },
      error: () => {
        this.messagesSignal.set(
          this.messagesSignal().map(x =>
            x.clientMessageId === request.clientMessageId
              ? {
                ...x,
               
                receiptStatus: 'failed'
              }
              : x
          )
        );
      }
    });
  }



  private normalizeMessage(message: MessageDto): MessageDto {
    return {
      ...message,
      attachments: message.attachments ?? [],
      receiptStatus: message.receiptStatus ?? 'sent',
      editSyncStatus: message.editSyncStatus ?? 'idle',
      deleteSyncStatus: message.deleteSyncStatus ?? 'idle'
    };
  }

  private isSameMessage(a: MessageDto, b: MessageDto): boolean {
    if (a.id === b.id) {
      return true;
    }

    if (
      a.clientMessageId &&
      b.clientMessageId &&
      a.clientMessageId === b.clientMessageId
    ) {
      return true;
    }

    return false;
  }

  private isLocalPendingMessage(message: MessageDto): boolean {
    return (
      message.receiptStatus === 'sending' ||
      message.receiptStatus === 'failed' ||
      message.editSyncStatus === 'syncing' ||
      message.editSyncStatus === 'failed' ||
      message.deleteSyncStatus === 'syncing' ||
      message.deleteSyncStatus === 'failed' ||
      (message.attachments ?? []).some(attachment => attachment.isLocalPending)
    );
  }


  handleMessageCreated(event: MessageCreatedEvent): void {
    const current = this.messagesSignal();

    const existingIndex = current.findIndex(message =>
      message.id === event.id ||
      message.clientMessageId === event.clientMessageId
    );

    const message: MessageDto = {
      id: event.id,
      conversationId: event.conversationId,
      sequenceNumber: event.sequenceNumber,
      senderUserId: event.senderUserId,
      body: event.body,
      replyToMessageId: event.replyToMessageId,
      replyToSequenceNumber: event.replyToSequenceNumber,
      createdAtUtc: event.createdAtUtc,
      editedAtUtc: null,
      deletedAtUtc: null,
      attachments: event.attachments,
      clientMessageId: event.clientMessageId ?? undefined,
      receiptStatus: 'sent',
      forwardedFromMessageId: event.forwardedFromMessageId,
      forwardedFromUserId: event.forwardedFromUserId,
      forwardedFromDisplayName: event.forwardedFromDisplayName
    };

    if (existingIndex >= 0) {
      const updated = [...current];
      updated[existingIndex] = message;
      this.messagesSignal.set(this.sortMessages(updated));

      void this.offlineMessages.save(message);
      return;
    }

    this.messagesSignal.set(
      this.sortMessages([...current, message])
    );

    void this.offlineMessages.save(message);
  }

  private sortMessages(messages: MessageDto[]): MessageDto[] {
    return [...messages].sort((a, b) => {
      const aSeq = a.sequenceNumber > 0 ? a.sequenceNumber : Number.MAX_SAFE_INTEGER;
      const bSeq = b.sequenceNumber > 0 ? b.sequenceNumber : Number.MAX_SAFE_INTEGER;

      if (aSeq !== bSeq) {
        return aSeq - bSeq;
      }

      return new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime();
    });
  }

  handleMessageEdited(event: {
    id: string;
    body: string;
    editedAtUtc: string | null;
    editSyncStatus?: 'idle' | 'syncing' | 'failed';
  }): void {


    
    let updatedMessage: MessageDto | null = null;

    this.messagesSignal.set(
      this.messagesSignal().map(message => {
        if (message.id !== event.id) {
          return message;
        }

        updatedMessage = {
          ...message,
          body: event.body,
          editedAtUtc: event.editedAtUtc,
          editSyncStatus: event.editSyncStatus ?? message.editSyncStatus ?? 'idle'
        };

        return updatedMessage;
      })
    );

    if (updatedMessage) {
      void this.offlineMessages.save(updatedMessage);
    }
  }

  handleMessageDeleted(event: {
    id: string;
    deletedAtUtc: string;
  }): void {
    let updatedMessage: MessageDto | null = null;

    this.messagesSignal.set(
      this.messagesSignal().map(message => {
        if (message.id !== event.id) {
          return message;
        }

        updatedMessage = {
          ...message,
          body: '',
          attachments: [],
          deletedAtUtc: event.deletedAtUtc
        };

        return updatedMessage;
      })
    );

    if (updatedMessage) {
      void this.offlineMessages.save(updatedMessage);
    }
  }


  getLastSequenceNumber(): number | null {
    const realSequenceNumbers = this.messagesSignal()
      .map(x => x.sequenceNumber)
      .filter(x => x > 0 && Number.isSafeInteger(x));

    if (realSequenceNumbers.length === 0) {
      return null;
    }

    return Math.max(...realSequenceNumbers);
  }

  handleMessageDelivered(conversationId: string, sequenceNumber: number): void {
    this.messagesSignal.set(
      this.messagesSignal().map(message =>
        message.conversationId === conversationId &&
          message.sequenceNumber <= sequenceNumber &&
          message.receiptStatus !== 'seen'
          ? {
            ...message,
            receiptStatus: 'delivered'
          }
          : message
      )
    );
  }

  handleMessageSeen(conversationId: string, sequenceNumber: number): void {
    this.messagesSignal.set(
      this.messagesSignal().map(message =>
        message.conversationId === conversationId &&
          message.sequenceNumber <= sequenceNumber
          ? {
            ...message,
            receiptStatus: 'seen'
          }
          : message
      )
    );
  }


  loadAround(
    conversationId: string,
    sequenceNumber: number,
    radius = 50
  ): Observable<void> {
    this.isLoadingSignal.set(true);

    return this.messagesApi
      .getConversationMessagesAround(conversationId, sequenceNumber, radius)
      .pipe(
        map(response => this.sortMessages(response)),
        tap(messages => {
          this.messagesSignal.set(messages);
        }),
        finalize(() => {
          this.isLoadingSignal.set(false);
        }),
        map(() => void 0)
      );
  }


  retryMessage(message: MessageDto): void {


    const isDeleteRetry =
      message.deleteSyncStatus === 'failed' ||
      message.deleteSyncStatus === 'syncing';

    if (isDeleteRetry) {
      this.messagesSignal.set(
        this.messagesSignal().map(x =>
          x.id === message.id
            ? { ...x, deleteSyncStatus: 'syncing' }
            : x
        )
      );

      void this.offlineMessages.save({
        ...message,
        deleteSyncStatus: 'syncing'
      }, 'pending');

void this.pendingOperations.upsertByMessageId(
  'deleteMessage',
  message.id,
  {
    messageId: message.id,
    deletedAtUtc: new Date().toISOString()
  }
);

      void this.offlineSync.forceSyncPendingOperations();
      return;
    }


    const isEditRetry =
      message.editSyncStatus === 'failed' ||
      message.editSyncStatus === 'syncing';


    if (isEditRetry) {
      this.messagesSignal.set(
        this.messagesSignal().map(x =>
          x.id === message.id
            ? {
              ...x,
              editSyncStatus: 'syncing'
            }
            : x
        )
      );


      void this.pendingOperations.add('editMessage', {
        messageId: message.id,
        body: message.body,
        editedAtUtc: message.editedAtUtc ?? new Date().toISOString()
      });

      void this.offlineSync.forceSyncPendingOperations();
      return;
    }

    const hasClientMessageId = !!message.clientMessageId;

    const isPendingSendRetry =
      hasClientMessageId &&
      (message.attachments ?? []).length === 0 &&
      !isEditRetry;

    const isPendingAttachmentRetry =
      hasClientMessageId &&
      (message.attachments ?? []).length > 0 &&
      !isEditRetry;

    this.messagesSignal.set(
      this.messagesSignal().map(x => {
        const isSameMessage =
          x.id === message.id ||
          (
            hasClientMessageId &&
            x.clientMessageId === message.clientMessageId
          );

        if (!isSameMessage) {
          return x;
        }

        return {
          ...x,
          receiptStatus: isEditRetry ? x.receiptStatus : 'sending',
          editSyncStatus: isEditRetry ? 'syncing' : x.editSyncStatus
        };
      })
    );

    if (isEditRetry) {
      void this.pendingOperations.add('editMessage', {
        messageId: message.id,
        body: message.body,
        editedAtUtc: message.editedAtUtc ?? new Date().toISOString()
      });

      void this.offlineSync.forceSyncPendingOperations();
      return;
    }

    if (isPendingSendRetry) {
      void this.pendingOperations.add('sendMessage', {
        conversationId: message.conversationId,
        clientMessageId: message.clientMessageId!,
        body: message.body,
        replyToMessageId: message.replyToMessageId,
        attachments: []
      });

      void this.offlineSync.forceSyncPendingOperations();
      return;
    }

    if (isPendingAttachmentRetry) {
      void this.offlineSync.forceSyncPendingOperations();
      return;
    }

    void this.offlineSync.forceSyncPendingOperations();
  }


  markMessageEditAsFailed(messageId: string): void {
    this.messagesSignal.set(
      this.messagesSignal().map(message =>
        message.id === messageId
          ? {
            ...message,
            editSyncStatus: 'failed'
          }
          : message
      )
    );
  }


  markMessageAsFailed(identifier: string): void {
    this.messagesSignal.set(
      this.messagesSignal().map(message =>
        message.id === identifier || message.clientMessageId === identifier
          ? {
            ...message,
            receiptStatus: 'failed',
            editSyncStatus:
              message.editSyncStatus === 'syncing'
                ? 'failed'
                : message.editSyncStatus
          }
          : message
      )
    );
  }
  sendOfflineMessageWithLocalAttachments(input: {
    conversationId: string;
    clientMessageId: string;
    body: string;
    replyToMessageId: string | null;
    files: File[];
  }): void {
    const currentUserId = this.authService.user()?.id;

    if (!currentUserId) {
      return;
    }

    const createdAtUtc = new Date().toISOString();

    const optimisticMessage: MessageDto = {
      id: input.clientMessageId,
      conversationId: input.conversationId,
      sequenceNumber: -1,
      senderUserId: currentUserId,
      body: input.body,
      replyToMessageId: input.replyToMessageId,
      replyToSequenceNumber: null,
      createdAtUtc,
      editedAtUtc: null,
      deletedAtUtc: null,
      attachments: input.files.map(file => ({
        id: crypto.randomUUID(),
        fileName: file.name || 'file',
        contentType: file.type || 'application/octet-stream',
        size: file.size,
        storagePath: '',
        localPreviewUrl: file.type.startsWith('image/')
          ? URL.createObjectURL(file)
          : undefined
      } as any)),
      clientMessageId: input.clientMessageId,
      receiptStatus: 'sending',

    };

    this.messagesSignal.set(
      this.sortMessages([...this.messagesSignal(), optimisticMessage])
    );

    void this.offlineMessages.save(optimisticMessage, 'pending');

    void this.pendingOperations.add('sendMessageWithAttachments', {
      conversationId: input.conversationId,
      clientMessageId: input.clientMessageId,
      body: input.body,
      replyToMessageId: input.replyToMessageId,
      files: input.files
    });
  }

}