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
import { Observable, catchError, finalize, from, map, of, switchMap, tap } from 'rxjs';
import { ConversationsState } from '../conversations/conversations.state';
import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';
import { OfflineFilesRepository } from '../../core/offline/offline-files.repository';
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

  load(conversationId: string): Observable<void> {
    this.isLoadingSignal.set(true);

    return from(this.offlineMessages.getByConversationId(conversationId)).pipe(
      tap(cachedMessages => {
        console.log('cached messages for conversation:', conversationId, cachedMessages);

        this.messagesSignal.set(this.sortMessages(cachedMessages));
      }),
      switchMap(() => {
        if (!navigator.onLine) {
          return of(null);
        }

        return this.messagesApi.getConversationMessages(conversationId, 50).pipe(
          catchError(error => {
            console.error('load messages from API failed:', error);
            return of(null);
          })
        );
      }),
      tap(response => {
        if (!response) {
          return;
        }

        const sorted = this.sortMessages(response);
        this.messagesSignal.set(sorted);

        void this.offlineMessages.saveMany(sorted);
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
      void this.pendingOperations.add('sendMessage', request);
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
                status: 'failed',
                receiptStatus: 'failed'
              }
              : x
          )
        );
      }
    });
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
          editedAtUtc: event.editedAtUtc
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
  if (!message.clientMessageId) {
    return;
  }

  this.messagesSignal.set(
    this.messagesSignal().map(x =>
      x.clientMessageId === message.clientMessageId
        ? {
            ...x,
            status: 'sending',
            receiptStatus: 'sending'
          }
        : x
    )
  );

  void this.pendingOperations.add('sendMessage', {
    conversationId: message.conversationId,
    clientMessageId: message.clientMessageId,
    body: message.body,
    replyToMessageId: message.replyToMessageId,
    attachments: message.attachments
  });
}


markMessageAsFailed(clientMessageId: string): void {
  this.messagesSignal.set(
    this.messagesSignal().map(message =>
      message.clientMessageId === clientMessageId
        ? {
            ...message,
            status: 'failed',
            receiptStatus: 'failed'
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