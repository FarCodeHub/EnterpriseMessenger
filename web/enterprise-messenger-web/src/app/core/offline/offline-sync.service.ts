import { Injectable, effect, inject } from '@angular/core';

import { NetworkStatusService } from './network-status.service';
import { PendingOperationsRepository } from './pending-operations.repository';

@Injectable({
  providedIn: 'root'
})
export class OfflineSyncService {
  private readonly networkStatus = inject(NetworkStatusService);
  private readonly pendingOperations = inject(PendingOperationsRepository);

  private isSyncing = false;

  constructor() {
    void this.pendingOperations.resetProcessingToPending();

    effect(() => {
      if (this.networkStatus.isOnline()) {
        void this.syncPendingOperations();
      }
    });
  }

  async syncPendingOperations(): Promise<void> {
    if (this.isSyncing) {
      return;
    }

    if (!this.networkStatus.isOnline()) {
      return;
    }

    this.isSyncing = true;

    try {
      const operations = await this.pendingOperations.getPending();

      for (const operation of operations) {
        await this.pendingOperations.markProcessing(operation.id);

        try {
          console.log('Sync pending operation:', operation);

          // Next steps:
          // sendMessage
          // editMessage
          // deleteMessage
          // markDelivered
          // markSeen

          await this.pendingOperations.remove(operation.id);
        } catch {
          await this.pendingOperations.markFailed(operation.id);
        }
      }
    } finally {
      this.isSyncing = false;
    }
  }
}