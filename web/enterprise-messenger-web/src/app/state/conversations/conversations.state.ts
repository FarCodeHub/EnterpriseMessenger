import { Injectable, inject, signal } from '@angular/core';

import { ConversationsApiService } from '../../core/services/conversations-api.service';
import { ConversationUpdatedEvent } from '../../core/signalr/chat-signalr.models';
import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';
import { OfflineConversationsRepository } from '../../core/offline/offline-conversations.repository';
@Injectable({
  providedIn: 'root'
})
export class ConversationsState {
  private readonly conversationsApi = inject(ConversationsApiService);
private readonly offlineConversations = inject(OfflineConversationsRepository);
  private readonly conversationsSignal = signal<ConversationListItemDto[]>([]);
  private readonly isLoadingSignal = signal(false);

  readonly conversations = this.conversationsSignal.asReadonly();
  readonly isLoading = this.isLoadingSignal.asReadonly();

load(): void {
  this.isLoadingSignal.set(true);

  void this.offlineConversations.getAll()
    .then(cachedConversations => {
      if (cachedConversations.length > 0) {
        this.conversationsSignal.set(cachedConversations);
      }

      if (!navigator.onLine) {
        this.isLoadingSignal.set(false);
        return;
      }

      this.conversationsApi.getMyConversations()
        .subscribe({
          next: conversations => {
            this.conversationsSignal.set(conversations);

            void this.offlineConversations.saveMany(conversations);

            this.isLoadingSignal.set(false);
          },
          error: () => {
            this.isLoadingSignal.set(false);
          }
        });
    });
}
  getById(conversationId: string) {
    return this.conversationsSignal()
      .find(conversation => conversation.id === conversationId) ?? null;
  }


  handleConversationUpdated(event: ConversationUpdatedEvent): void {
    
 
    const current = this.conversationsSignal();

    const index = current.findIndex(x => x.id === event.conversationId);

    if (index < 0) {
      return;
    }

    const updated = [...current];

    const conversation = updated[index];

 
    updated[index] = {
      ...conversation,
      lastMessageId: event.lastMessageId,
      lastMessagePreview: event.body,
      lastMessageSenderUserId: event.senderUserId,
      lastMessageSenderDisplayName: event.senderDisplayName,
      lastMessageAtUtc: event.createdAtUtc
    };
 

    


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




  updateLastMessageLocally(input: {
  conversationId: string;
  body: string;
  senderUserId: string;
  senderDisplayName?: string | null;
  createdAtUtc: string;
}): void {
  const updated = this.conversationsSignal().map(conversation =>
    conversation.id === input.conversationId
      ? {
          ...conversation,
          lastMessagePreview: input.body,
          lastMessageAtUtc: input.createdAtUtc,
          lastMessageSenderUserId: input.senderUserId,
          lastMessageSenderDisplayName:
            input.senderDisplayName ?? conversation.lastMessageSenderDisplayName
        }
      : conversation
  );

  this.conversationsSignal.set(
    [...updated].sort((a, b) =>
      new Date(b.lastMessageAtUtc ?? '').getTime() -
      new Date(a.lastMessageAtUtc ?? '').getTime()
    )
  );

  const updatedConversation = updated.find(x => x.id === input.conversationId);

  if (updatedConversation) {
    void this.offlineConversations.save(updatedConversation);
  }
}



}