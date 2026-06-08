import { Injectable } from '@angular/core';

import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';
import { appLocalDb } from './app-local-db';

@Injectable({
  providedIn: 'root'
})
export class OfflineConversationsRepository {
  async saveMany(conversations: ConversationListItemDto[]): Promise<void> {
    await appLocalDb.conversations.bulkPut(conversations);
  }

  async getAll(): Promise<ConversationListItemDto[]> {
    const conversations = await appLocalDb.conversations
      .orderBy('lastMessageAtUtc')
      .reverse()
      .toArray();

    return conversations;
  }

  async getById(id: string): Promise<ConversationListItemDto | undefined> {
    return appLocalDb.conversations.get(id);
  }

  async save(conversation: ConversationListItemDto): Promise<void> {
    await appLocalDb.conversations.put(conversation);
  }

  async clear(): Promise<void> {
    await appLocalDb.conversations.clear();
  }
}