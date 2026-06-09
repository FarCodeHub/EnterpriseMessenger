import Dexie, { Table } from 'dexie';

import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';
import { MessageDto } from '../../features/messages/models/message.dto';

export type PendingOperationType =
  | 'sendMessage'
  | 'sendMessageWithAttachments'
  | 'editMessage'
  | 'deleteMessage'
  | 'markDelivered'
  | 'markSeen';

export interface LocalMessage extends MessageDto {
  syncStatus?: 'synced' | 'pending' | 'failed';
}

export interface PendingOperation {
  id: string;
  type: PendingOperationType;
  payload: unknown;
  createdAtUtc: string;
  retryCount: number;
  status: 'pending' | 'processing' | 'failed';
}

export interface SyncState {
  key: string;
  value: string;
}

export interface LocalPendingFile {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
  blob: Blob;
  createdAtUtc: string;
}

export class AppLocalDb extends Dexie {
    pendingFiles!: Table<LocalPendingFile, string>;
  conversations!: Table<ConversationListItemDto, string>;
  messages!: Table<LocalMessage, string>;
  pendingOperations!: Table<PendingOperation, string>;
  syncState!: Table<SyncState, string>;

  constructor() {
    super('EnterpriseMessengerDb');

    this.version(1).stores({
      conversations: 'id, lastMessageAtUtc',
      messages: 'id, conversationId, sequenceNumber, clientMessageId, createdAtUtc',
      pendingOperations: 'id, type, status, createdAtUtc',
      syncState: 'key',
      pendingFiles: 'id, createdAtUtc'
    });
  }
}

export const appLocalDb = new AppLocalDb();