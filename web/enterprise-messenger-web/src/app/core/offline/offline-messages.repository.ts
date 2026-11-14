import { Injectable } from '@angular/core';

import { MessageDto } from '../../features/messages/models/message.dto';
import { LocalMessage, localDb } from './app-local-db';

@Injectable({
  providedIn: 'root'
})
export class OfflineMessagesRepository {

  
async saveMany(messages: MessageDto[]): Promise<void> {
  for (const message of messages) {
    if (message.clientMessageId) {
      await this.deleteByClientMessageId(message.clientMessageId);
    } else {
      await this.deletePendingDuplicateOfServerMessage(message);
    }

    await localDb().messages.put({
      ...message,
      attachments: message.attachments ?? [],
      syncStatus: 'synced'
    });
  }
}
async deletePendingDuplicateOfServerMessage(serverMessage: MessageDto): Promise<void> {
  const candidates = await localDb().messages
    .where('conversationId')
    .equals(serverMessage.conversationId)
    .toArray();

  const serverTime = new Date(serverMessage.createdAtUtc).getTime();

  const duplicates = candidates.filter(local => {
    const localTime = new Date(local.createdAtUtc).getTime();
    const timeDiffMs = Math.abs(serverTime - localTime);

    return (
      local.id !== serverMessage.id &&
      local.senderUserId === serverMessage.senderUserId &&
      local.body === serverMessage.body &&
      local.sequenceNumber < 0 &&
      timeDiffMs < 5 * 60 * 1000
    );
  });

  await localDb().messages.bulkDelete(
    duplicates.map(x => x.id)
  );
}

  async deleteByClientMessageId(clientMessageId: string): Promise<void> {
  const messages = await localDb().messages
    .where('clientMessageId')
    .equals(clientMessageId)
    .toArray();

  await localDb().messages.bulkDelete(messages.map(x => x.id));
}

async deleteById(id: string): Promise<void> {
  await localDb().messages.delete(id);
}


  async save(message: MessageDto, syncStatus: LocalMessage['syncStatus'] = 'synced'): Promise<void> {
    await localDb().messages.put({
      ...message,
      syncStatus
    });
  }

async getByConversationId(conversationId: string): Promise<LocalMessage[]> {
  return localDb().messages
    .where('conversationId')
    .equals(conversationId)
    .toArray();
}

  async deleteByConversationId(conversationId: string): Promise<void> {
    const messages = await localDb().messages
      .where('conversationId')
      .equals(conversationId)
      .toArray();

    await localDb().messages.bulkDelete(messages.map(x => x.id));
  }

  async clear(): Promise<void> {
    await localDb().messages.clear();
  }
}