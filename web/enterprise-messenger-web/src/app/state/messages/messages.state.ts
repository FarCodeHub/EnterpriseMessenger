import { Injectable, inject, signal } from '@angular/core';
import { SendMessageRequest } from '../../features/messages/models/send-message.request';
import { MessagesApiService } from '../../core/services/messages-api.service';
import { MessageDto } from '../../features/messages/models/message.dto';
import {
  MessageCreatedEvent,
  MessageDeletedEvent,
  MessageEditedEvent
} from '../../core/signalr/chat-signalr.models';
 
import { OfflineMessagesRepository } from '../../core/offline/offline-messages.repository';
import { Observable, finalize, from, map, of, switchMap, tap } from 'rxjs';
@Injectable({
  providedIn: 'root'
})
export class MessagesState {
  private readonly messagesApi = inject(MessagesApiService);
  private readonly offlineMessages = inject(OfflineMessagesRepository);
  private readonly messagesSignal = signal<MessageDto[]>([]);
  private readonly isLoadingSignal = signal(false);

  readonly messages = this.messagesSignal.asReadonly();
  readonly isLoading = this.isLoadingSignal.asReadonly();

 


load(conversationId: string): Observable<void> {
  this.isLoadingSignal.set(true);

  return from(this.offlineMessages.getByConversationId(conversationId)).pipe(
    tap(cachedMessages => {
      if (cachedMessages.length > 0) {
        this.messagesSignal.set(this.sortMessages(cachedMessages));
      }
    }),
    switchMap(() => {
      if (!navigator.onLine) {
        return of(null);
      }

      return this.messagesApi.getConversationMessages(conversationId, 50);
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
    const optimisticMessage: MessageDto = {
      id: request.clientMessageId,
      conversationId: request.conversationId,
      sequenceNumber: -1,
      senderUserId: '',
      body: request.body,
      replyToMessageId: request.replyToMessageId,
      replyToSequenceNumber: null,
      createdAtUtc: new Date().toISOString(),
      editedAtUtc: null,
      deletedAtUtc: null,
      attachments: [],
      clientMessageId: request.clientMessageId,
      receiptStatus: 'sending'
    };

    this.messagesSignal.set([...this.messagesSignal(), optimisticMessage]);

    this.messagesApi.sendMessage(request).subscribe({
      next: message => {
        console.log('send message response:', message);
        console.log('send message attachments:', message.attachments);

        this.messagesSignal.set(
          this.messagesSignal().map(x =>
            x.clientMessageId === request.clientMessageId
              ? {
                ...message,
                clientMessageId: request.clientMessageId,
                receiptStatus: 'sent'
              }
              : x
          )
        );
      },
      error: () => {
        this.messagesSignal.set(
          this.messagesSignal().map(x =>
            x.clientMessageId === request.clientMessageId
              ? { ...x, status: 'failed' }
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
      if (a.sequenceNumber !== b.sequenceNumber) {
        return a.sequenceNumber - b.sequenceNumber;
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

}