import { Injectable, inject, signal } from '@angular/core';
import { SendMessageRequest } from '../../features/messages/models/send-message.request';
import { MessagesApiService } from '../../core/services/messages-api.service';
import { MessageDto } from '../../features/messages/models/message.dto';
import { MessageCreatedEvent } from '../../core/signalr/chat-signalr.models';
@Injectable({
  providedIn: 'root'
})
export class MessagesState {
  private readonly messagesApi = inject(MessagesApiService);

  private readonly messagesSignal = signal<MessageDto[]>([]);
  private readonly isLoadingSignal = signal(false);

  readonly messages = this.messagesSignal.asReadonly();
  readonly isLoading = this.isLoadingSignal.asReadonly();

load(conversationId: string): void {
  this.isLoadingSignal.set(true);

  this.messagesApi.getConversationMessages(conversationId)
    .subscribe({
      next: response => {
        this.messagesSignal.set(
          this.sortMessages(response)
        );

        this.isLoadingSignal.set(false);
      },
      error: () => {
        this.messagesSignal.set([]);
        this.isLoadingSignal.set(false);
      }
    });
}

  clear(): void {
    this.messagesSignal.set([]);
  }
 
send(request: SendMessageRequest): void {
  const optimisticMessage: MessageDto = {
    id: request.clientMessageId,
    conversationId: request.conversationId,
    sequenceNumber: Number.MAX_SAFE_INTEGER,
    senderUserId: '',
    body: request.body,
    replyToMessageId: request.replyToMessageId,
    replyToSequenceNumber: null,
    createdAtUtc: new Date().toISOString(),
    editedAtUtc: null,
    deletedAtUtc: null,
    attachments: [],
    clientMessageId: request.clientMessageId,
    status: 'sending'
  };

  this.messagesSignal.set([...this.messagesSignal(), optimisticMessage]);

  this.messagesApi.sendMessage(request).subscribe({
    next: message => {
      this.messagesSignal.set(
        this.messagesSignal().map(x =>
          x.clientMessageId === request.clientMessageId
            ? { ...message, clientMessageId: request.clientMessageId, status: 'sent' }
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




}