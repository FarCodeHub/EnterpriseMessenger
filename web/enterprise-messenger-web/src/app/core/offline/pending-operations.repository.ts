import { Injectable } from '@angular/core';

import {
  PendingOperation,
  PendingOperationType,
  appLocalDb
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

    await appLocalDb.pendingOperations.add(operation);

    return operation;
  }

  async getPending(): Promise<PendingOperation[]> {
    return appLocalDb.pendingOperations
      .where('status')
      .equals('pending')
      .sortBy('createdAtUtc');
  }

  async markProcessing(id: string): Promise<void> {
    await appLocalDb.pendingOperations.update(id, {
      status: 'processing'
    });
  }

  async markFailed(id: string): Promise<void> {
    const operation = await appLocalDb.pendingOperations.get(id);

    await appLocalDb.pendingOperations.update(id, {
      status: 'failed',
      retryCount: (operation?.retryCount ?? 0) + 1
    });
  }

  async remove(id: string): Promise<void> {
    await appLocalDb.pendingOperations.delete(id);
  }

  async resetProcessingToPending(): Promise<void> {
    const processing = await appLocalDb.pendingOperations
      .where('status')
      .equals('processing')
      .toArray();

    await appLocalDb.pendingOperations.bulkPut(
      processing.map(operation => ({
        ...operation,
        status: 'pending' as const
      }))
    );
  }
}