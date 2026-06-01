import { Injectable, inject, signal } from '@angular/core';
import { SendMessageRequest } from '../../features/messages/models/send-message.request';
import { MessagesApiService } from '../../core/services/messages-api.service';
import { MessageDto } from '../../features/messages/models/message.dto';
import {
  MessageCreatedEvent,
  MessageDeletedEvent,
  MessageEditedEvent
} from '../../core/signalr/chat-signalr.models';
import { Observable, tap, finalize, map } from 'rxjs';
 @Injectable({
  providedIn: 'root'
})
export class MessagesState {
  private readonly messagesApi = inject(MessagesApiService);

  private readonly messagesSignal = signal<MessageDto[]>([]);
  private readonly isLoadingSignal = signal(false);

  readonly messages = this.messagesSignal.asReadonly();
  readonly isLoading = this.isLoadingSignal.asReadonly();

load(conversationId: string): Observable<void> {
  this.isLoadingSignal.set(true);

  return this.messagesApi.getConversationMessages(conversationId, 50).pipe(
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

  const message = {
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
    status: 'sent' as const
  };

  if (existingIndex >= 0) {
    const updated = [...current];
    updated[existingIndex] = message;
    this.messagesSignal.set(this.sortMessages(updated));
    return;
  }

  this.messagesSignal.set(
    this.sortMessages([...current, message])
  );
}

private sortMessages(messages: MessageDto[]): MessageDto[] {
  return [...messages].sort((a, b) => {
    if (a.sequenceNumber !== b.sequenceNumber) {
      return a.sequenceNumber - b.sequenceNumber;
    }

    return new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime();
  });
}

handleMessageEdited(event: MessageEditedEvent): void {
  this.messagesSignal.set(
    this.messagesSignal().map(message =>
      message.id === event.id
        ? {
            ...message,
            body: event.body,
            editedAtUtc: event.editedAtUtc
          }
        : message
    )
  );
}

handleMessageDeleted(event: MessageDeletedEvent): void {
  this.messagesSignal.set(
    this.messagesSignal().map(message =>
      message.id === event.id
        ? {
            ...message,
            deletedAtUtc: event.deletedAtUtc,
            body: ''
          }
        : message
    )
  );
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