import { Injectable, inject, signal } from '@angular/core';

import { ConversationsApiService } from '../../core/services/conversations-api.service';
import { ConversationUpdatedEvent } from '../../core/signalr/chat-signalr.models';
import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';

@Injectable({
  providedIn: 'root'
})
export class ConversationsState {
  private readonly conversationsApi = inject(ConversationsApiService);

  private readonly conversationsSignal = signal<ConversationListItemDto[]>([]);
  private readonly isLoadingSignal = signal(false);

  readonly conversations = this.conversationsSignal.asReadonly();
  readonly isLoading = this.isLoadingSignal.asReadonly();

  load(): void {
    this.isLoadingSignal.set(true);

    this.conversationsApi.getMyConversations()
      .subscribe({
        next: response => {
          this.conversationsSignal.set(response);
          this.isLoadingSignal.set(false);
        },
        error: () => {
          this.isLoadingSignal.set(false);
        }
      });
  }
  getById(conversationId: string) {
    return this.conversationsSignal()
      .find(conversation => conversation.id === conversationId) ?? null;
  }


  handleConversationUpdated(event: ConversationUpdatedEvent): void {
    
console.log(event);
    const current = this.conversationsSignal();

    const index = current.findIndex(x => x.id === event.conversationId);

    if (index < 0) {
      return;
    }

    const updated = [...current];

    const conversation = updated[index];

    console.log('before update:', conversation.lastMessageSenderDisplayName);
console.log('event sender:', event.senderDisplayName);
    updated[index] = {
      ...conversation,
      lastMessageId: event.lastMessageId,
      lastMessagePreview: event.body,
      lastMessageSenderUserId: event.senderUserId,
      lastMessageSenderDisplayName: event.senderDisplayName,
      lastMessageAtUtc: event.createdAtUtc
    };
console.log('after update:', updated[index].lastMessageSenderDisplayName);

    


    updated.sort((a, b) => {
      const aTime = a.lastMessageAtUtc
        ? new Date(a.lastMessageAtUtc).getTime()
        : 0;

      const bTime = b.lastMessageAtUtc
        ? new Date(b.lastMessageAtUtc).getTime()
        : 0;

      return bTime - aTime;
    });

    this.conversationsSignal.set(updated);
  }

updateOtherParticipantDelivered(
  conversationId: string,
  sequenceNumber: number
): void {
  this.conversationsSignal.set(
    this.conversationsSignal().map(conversation => {
      if (conversation.id !== conversationId) {
        return conversation;
      }

      return {
        ...conversation,
        otherParticipantLastDeliveredSequenceNumber: Math.max(
          conversation.otherParticipantLastDeliveredSequenceNumber ?? 0,
          sequenceNumber
        )
      };
    })
  );
}

updateOtherParticipantSeen(
  conversationId: string,
  sequenceNumber: number
): void {
  this.conversationsSignal.set(
    this.conversationsSignal().map(conversation => {
      if (conversation.id !== conversationId) {
        return conversation;
      }

      return {
        ...conversation,
        otherParticipantLastReadSequenceNumber: Math.max(
          conversation.otherParticipantLastReadSequenceNumber ?? 0,
          sequenceNumber
        )
      };
    })
  );
}

}