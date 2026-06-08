import { Injectable, effect, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { NetworkStatusService } from './network-status.service';
import { PendingOperationsRepository } from './pending-operations.repository';
import { MessagesApiService } from '../services/messages-api.service';
import { OfflineMessagesRepository } from './offline-messages.repository';
import { SendMessageRequest } from '../../features/messages/models/send-message.request';
import { Subject } from 'rxjs';
import { MessageDto } from '../../features/messages/models/message.dto';
import { EditMessageRequest } from '../../features/messages/models/edit-message.request';
@Injectable({
    providedIn: 'root'
})
export class OfflineSyncService {
    private readonly networkStatus = inject(NetworkStatusService);
    private readonly pendingOperations = inject(PendingOperationsRepository);
    private readonly messagesApi = inject(MessagesApiService);
    private readonly offlineMessages = inject(OfflineMessagesRepository);
    readonly messageSynced$ = new Subject<MessageDto>();
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
                    if (operation.type === 'sendMessage') {
                        await this.syncSendMessage(operation.payload as SendMessageRequest);
                    }

                    if (operation.type === 'editMessage') {
                        const payload = operation.payload as {
                            messageId: string;
                            body: string;
                        };

                        await this.syncEditMessage(payload.messageId, {
                            body: payload.body
                        });
                    }

                    await this.pendingOperations.remove(operation.id);
                } catch {
                    await this.pendingOperations.markFailed(operation.id);
                }
            }
        } finally {
            this.isSyncing = false;
        }
    }

    private async syncSendMessage(request: SendMessageRequest): Promise<void> {
        const message = await firstValueFrom(
            this.messagesApi.sendMessage(request)
        );

        const syncedMessage: MessageDto = {
            ...message,
            clientMessageId: request.clientMessageId,
            receiptStatus: 'sent'
        };

        await this.offlineMessages.save(syncedMessage);

        this.messageSynced$.next(syncedMessage);
    }

    private async syncEditMessage(
        messageId: string,
        request: EditMessageRequest
    ): Promise<void> {
        await firstValueFrom(
            this.messagesApi.editMessage(messageId, request)
        );
    }
}