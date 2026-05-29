import { Injectable, inject, signal } from '@angular/core';

import { ConversationsApiService } from '../../core/services/conversations-api.service';

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
}