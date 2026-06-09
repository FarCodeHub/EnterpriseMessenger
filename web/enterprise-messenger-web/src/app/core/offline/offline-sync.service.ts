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
import { FilesApiService } from '../services/files-api.service';
@Injectable({
    providedIn: 'root'
})
export class OfflineSyncService {
    private readonly filesApi = inject(FilesApiService);
    readonly messageSyncFailed$ = new Subject<string>();
    private readonly networkStatus = inject(NetworkStatusService);
    private readonly pendingOperations = inject(PendingOperationsRepository);
    private readonly messagesApi = inject(MessagesApiService);
    private readonly offlineMessages = inject(OfflineMessagesRepository);
    readonly messageSynced$ = new Subject<MessageDto>();
    private isSyncing = false;
    private retryTimer: ReturnType<typeof setTimeout> | null = null;
    private retryDelayMs = 3000;
    private readonly maxRetryDelayMs = 60000;

    constructor() {
        void this.pendingOperations.resetProcessingToPending();

        effect(() => {
            if (this.networkStatus.isOnline()) {
                void this.syncPendingOperations();
            }
        });
    }

    async syncPendingOperations(): Promise<void> {
        console.log('Sync started. Online:', this.networkStatus.isOnline());
        if (this.isSyncing) {
            return;
        }

        if (!this.networkStatus.isOnline()) {
            return;
        }

        this.isSyncing = true;

        try {
            const operations = await this.pendingOperations.getPending();
            console.log('Pending operations:', operations);

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

                    if (operation.type === 'sendMessageWithAttachments') {
                        await this.syncSendMessageWithAttachments(operation.payload as {
                            conversationId: string;
                            clientMessageId: string;
                            body: string;
                            replyToMessageId: string | null;
                            files: File[];
                        });
                    }

                    await this.pendingOperations.remove(operation.id);
                    this.retryDelayMs = 3000;
                } catch (error) {
                    console.warn('Pending operation sync failed:', operation, error);

                    await this.pendingOperations.markFailed(operation.id);

                    const updatedOperation = await this.pendingOperations.getById(operation.id);

                    if (
                        updatedOperation?.status === 'failed' &&
                        operation.type === 'sendMessage'
                    ) {
                        const payload = operation.payload as SendMessageRequest;

                        this.messageSyncFailed$.next(payload.clientMessageId);
                    } else {
                        this.scheduleRetry();
                    }
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


    private scheduleRetry(): void {
        if (!this.networkStatus.isOnline()) {
            return;
        }

        if (this.retryTimer) {
            return;
        }

        this.retryTimer = setTimeout(() => {
            this.retryTimer = null;
            void this.syncPendingOperations();
        }, this.retryDelayMs);

        this.retryDelayMs = Math.min(
            this.retryDelayMs * 2,
            this.maxRetryDelayMs
        );
    }

    private async syncSendMessageWithAttachments(input: {
        conversationId: string;
        clientMessageId: string;
        body: string;
        replyToMessageId: string | null;
        files: File[];
    }): Promise<void> {
        const uploadedAttachments = [];

        for (const file of input.files) {
            const uploaded = await firstValueFrom(
                this.filesApi.upload(file)
            );

            uploadedAttachments.push({
                fileId: uploaded.fileId,
                fileName: uploaded.fileName,
                contentType: uploaded.contentType,
                size: uploaded.size,
                storagePath: uploaded.storagePath
            });
        }

        const message = await firstValueFrom(
            this.messagesApi.sendMessage({
                conversationId: input.conversationId,
                clientMessageId: input.clientMessageId,
                body: input.body,
                replyToMessageId: input.replyToMessageId,
                attachments: uploadedAttachments
            })
        );

        const syncedMessage: MessageDto = {
            ...message,
            clientMessageId: input.clientMessageId,
            receiptStatus: 'sent',

        };

        await this.offlineMessages.save(syncedMessage);

        this.messageSynced$.next(syncedMessage);
    }
}