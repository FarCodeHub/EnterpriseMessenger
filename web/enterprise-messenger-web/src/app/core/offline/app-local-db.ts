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

  constructor(name: string) {
    super(name);

    this.version(1).stores({
      conversations: 'id, lastMessageAtUtc',
      messages: 'id, conversationId, sequenceNumber, clientMessageId, createdAtUtc',
      pendingOperations: 'id, type, status, createdAtUtc',
      syncState: 'key',
      pendingFiles: 'id, createdAtUtc'
    });
  }
}

/*
 * One IndexedDB database per signed-in user, so one user never reads another user's cached messages or sends their
 * unsent ones (the browser may be shared). Before sign-in an empty "anonymous" database is used.
 * AuthService switches the database on sign-in and deletes it on an explicit sign-out.
 */
const databaseNamePrefix = 'EnterpriseMessengerDb';
const anonymous = 'anonymous';

let currentOwner = anonymous;
let currentDb: AppLocalDb | null = null;

function databaseName(owner: string): string {
  return `${databaseNamePrefix}-${owner}`;
}

/** The current user's local database. */
export function localDb(): AppLocalDb {
  currentDb ??= new AppLocalDb(databaseName(currentOwner));
  return currentDb;
}

/** Switches to the database of this user (null: signed out). */
export function useLocalDbFor(userId: string | null): void {
  const owner = userId ?? anonymous;

  if (owner === currentOwner) {
    return;
  }

  currentDb?.close();
  currentDb = null;
  currentOwner = owner;
}

/** Deletes this user's local data (sign-out) and switches to the anonymous database. */
export async function deleteLocalDbFor(userId: string): Promise<void> {
  if (currentOwner === userId) {
    useLocalDbFor(null);
  }

  await Dexie.delete(databaseName(userId));
}

/** Deletes the database used before data was kept per user; it cannot be attributed to anyone. */
export function deleteSharedLegacyDb(): Promise<void> {
  return Dexie.delete(databaseNamePrefix);
}

/** Deletes what was written while nobody was signed in (e.g. by a request that finished after sign-out). */
export function deleteAnonymousDb(): Promise<void> {
  return currentOwner === anonymous ? Promise.resolve() : Dexie.delete(databaseName(anonymous));
}
