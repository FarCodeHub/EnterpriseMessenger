import { Injectable } from '@angular/core';

import {
  PendingOperation,
  PendingOperationType,
  localDb
} from './app-local-db';

@Injectable({
  providedIn: 'root'
})
export class PendingOperationsRepository {
  async add(
    type: PendingOperationType,
    payload: unknown
  ): Promise<PendingOperation> {
    const operation: PendingOperation = {
      id: crypto.randomUUID(),
      type,
      payload,
      createdAtUtc: new Date().toISOString(),
      retryCount: 0,
      status: 'pending'
    };

    await localDb().pendingOperations.add(operation);

    return operation;
  }


async upsertByClientMessageId(
  type: PendingOperationType,
  clientMessageId: string,
  payload: unknown
): Promise<void> {
  const existing = await localDb().pendingOperations
    .filter(operation =>
      operation.type === type &&
      JSON.stringify(operation.payload).includes(clientMessageId)
    )
    .first();

  if (existing) {
    await localDb().pendingOperations.put({
      ...existing,
      payload,
      status: 'pending',
      retryCount: 0
    });

    return;
  }

  await this.add(type, payload);
}


async upsertByMessageId(
  type: PendingOperationType,
  messageId: string,
  payload: unknown
): Promise<void> {
  const existing = await localDb().pendingOperations
    .filter(operation =>
      operation.type === type &&
      JSON.stringify(operation.payload).includes(messageId)
    )
    .first();

  if (existing) {
    await localDb().pendingOperations.put({
      ...existing,
      payload,
      status: 'pending',
      retryCount: 0
    });

    return;
  }

  await this.add(type, payload);
}

  async resetAllToPending(): Promise<void> {
  const operations = await localDb().pendingOperations.toArray();

  await localDb().pendingOperations.bulkPut(
    operations.map(operation => ({
      ...operation,
      status: 'pending' as const
    }))
  );
}

  async getPending(): Promise<PendingOperation[]> {
    return localDb().pendingOperations
      .where('status')
      .equals('pending')
      .sortBy('createdAtUtc');
  }

  async markProcessing(id: string): Promise<void> {
    await localDb().pendingOperations.update(id, {
      status: 'processing'
    });
  }


  async deleteByClientMessageId(clientMessageId: string): Promise<void> {
  const operations = await localDb().pendingOperations
    .filter(operation =>
      JSON.stringify(operation.payload).includes(clientMessageId)
    )
    .toArray();

  await localDb().pendingOperations.bulkDelete(
    operations.map(x => x.id)
  );
}

async markFailed(id: string): Promise<void> {
  const operation = await localDb().pendingOperations.get(id);
  const retryCount = (operation?.retryCount ?? 0) + 1;

  await localDb().pendingOperations.update(id, {
    status: retryCount >= 5 ? 'failed' : 'pending',
    retryCount
  });
}


async retryFailed(): Promise<void> {
  const failed = await localDb().pendingOperations
    .where('status')
    .equals('failed')
    .toArray();

  await localDb().pendingOperations.bulkPut(
    failed.map(operation => ({
      ...operation,
      status: 'pending' as const
    }))
  );
}

  async remove(id: string): Promise<void> {
    await localDb().pendingOperations.delete(id);
  }

  async resetProcessingToPending(): Promise<void> {
    const processing = await localDb().pendingOperations
      .where('status')
      .equals('processing')
      .toArray();

    await localDb().pendingOperations.bulkPut(
      processing.map(operation => ({
        ...operation,
        status: 'pending' as const
      }))
    );
  }

async getById(id: string): Promise<PendingOperation | undefined> {
  return localDb().pendingOperations.get(id);
}


}