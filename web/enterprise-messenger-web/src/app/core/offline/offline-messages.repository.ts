import { Injectable } from '@angular/core';

import { MessageDto } from '../../features/messages/models/message.dto';
import { LocalMessage, appLocalDb } from './app-local-db';

@Injectable({
  providedIn: 'root'
})
export class OfflineMessagesRepository {
  async saveMany(messages: MessageDto[]): Promise<void> {
    const localMessages: LocalMessage[] = messages.map(message => ({
      ...message,
      syncStatus: 'synced'
    }));

    await appLocalDb.messages.bulkPut(localMessages);
  }


  async deleteByClientMessageId(clientMessageId: string): Promise<void> {
  const messages = await appLocalDb.messages
    .where('clientMessageId')
    .equals(clientMessageId)
    .toArray();

  await appLocalDb.messages.bulkDelete(messages.map(x => x.id));
}

  async save(message: MessageDto, syncStatus: LocalMessage['syncStatus'] = 'synced'): Promise<void> {
    await appLocalDb.messages.put({
      ...message,
      syncStatus
    });
  }

async getByConversationId(conversationId: string): Promise<LocalMessage[]> {
  return appLocalDb.messages
    .where('conversationId')
    .equals(conversationId)
    .toArray();
}

  async deleteByConversationId(conversationId: string): Promise<void> {
    const messages = await appLocalDb.messages
      .where('conversationId')
      .equals(conversationId)
      .toArray();

    await appLocalDb.messages.bulkDelete(messages.map(x => x.id));
  }

  async clear(): Promise<void> {
    await appLocalDb.messages.clear();
  }


async deleteById(id: string): Promise<void> {
  await appLocalDb.messages.delete(id);
}

  
}