import { Injectable } from '@angular/core';

import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';
import { localDb } from './app-local-db';

@Injectable({
  providedIn: 'root'
})
export class OfflineConversationsRepository {
  async saveMany(conversations: ConversationListItemDto[]): Promise<void> {
    await localDb().conversations.bulkPut(conversations);
  }

  async getAll(): Promise<ConversationListItemDto[]> {
    const conversations = await localDb().conversations
      .orderBy('lastMessageAtUtc')
      .reverse()
      .toArray();

    return conversations;
  }

  async getById(id: string): Promise<ConversationListItemDto | undefined> {
    return localDb().conversations.get(id);
  }

  async save(conversation: ConversationListItemDto): Promise<void> {
    await localDb().conversations.put(conversation);
  }

  async clear(): Promise<void> {
    await localDb().conversations.clear();
  }
}