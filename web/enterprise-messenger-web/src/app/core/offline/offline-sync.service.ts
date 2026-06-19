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
import { OfflineFilesRepository } from './offline-files.repository';
@Injectable({
    providedIn: 'root'
})
export class OfflineSyncService {
    private syncRequested = false;
    readonly messageEditSyncFailed$ = new Subject<string>();
    private syncRequestTimer: ReturnType<typeof setTimeout> | null = null;
    private readonly offlineFiles = inject(OfflineFilesRepository);
    private readonly filesApi = inject(FilesApiService);
    readonly messageSyncFailed$ = new Subject<string>();
    private readonly networkStatus = inject(NetworkStatusService);
    private readonly pendingOperations = inject(PendingOperationsRepository);
    private readonly messagesApi = inject(MessagesApiService);
    private readonly offlineMessages = inject(OfflineMessagesRepository);
    readonly messageSynced$ = new Subject<MessageDto>();
    readonly messageEditSynced$ = new Subject<{
        messageId: string;
        body: string;
        editedAtUtc: string | null;
    }>();
    private isSyncing = false;
    private retryTimer: ReturnType<typeof setTimeout> | null = null;
    private retryDelayMs = 3000;
    private readonly maxRetryDelayMs = 60000;

    constructor() {
        void this.pendingOperations.resetProcessingToPending();

        setTimeout(() => {
            this.requestSync('app-startup');
        }, 2000);

        window.addEventListener('online', () => {
            this.requestSync('browser-online');
        });

        effect(() => {
            if (this.networkStatus.isOnline()) {
                this.requestSync('network-status-online');
            }
        });
    }

    requestSync(reason: string): void {


        if (this.syncRequested) {
            return;
        }

        this.syncRequested = true;

        if (this.syncRequestTimer) {
            clearTimeout(this.syncRequestTimer);
        }

        this.syncRequestTimer = setTimeout(() => {
            this.syncRequested = false;
            this.syncRequestTimer = null;

            void this.syncPendingOperations();
        }, 1500);
    }
    async forceSyncPendingOperations(): Promise<void> {
        if (this.isSyncing) {
            return;
        }

        if (!navigator.onLine && !this.networkStatus.isOnline()) {
            return;
        }



        await this.pendingOperations.resetAllToPending();

        if (this.retryTimer) {
            clearTimeout(this.retryTimer);
            this.retryTimer = null;
        }

        await this.syncPendingOperations();
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


                    if (operation.type === 'deleteMessage') {
                        const payload = operation.payload as {
                            messageId: string;
                            deletedAtUtc?: string;
                        };

                        await this.syncDeleteMessage(payload.messageId);
                    }


                    if (operation.type === 'sendMessage') {
                        await this.syncSendMessage(operation.payload as SendMessageRequest);
                    }

                    if (operation.type === 'editMessage') {
                        const payload = operation.payload as {
                            messageId: string;
                            body: string;
                            editedAtUtc?: string | null;
                        };

                        await this.syncEditMessage(payload.messageId, {
                            body: payload.body
                        }, payload.editedAtUtc);
                    }

                    if (operation.type === 'sendMessageWithAttachments') {

                        await this.syncSendMessageWithAttachments(operation.payload as {
                            conversationId: string;
                            clientMessageId: string;
                            body: string;
                            replyToMessageId: string | null;
                            fileIds: string[];
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
                        (
                            operation.type === 'sendMessage' ||
                            operation.type === 'sendMessageWithAttachments' ||
                            operation.type === 'editMessage'
                        )
                    ) {

                        if (
                            updatedOperation?.status === 'failed' &&
                            operation.type === 'editMessage'
                        ) {
                            const payload = operation.payload as { messageId: string };

                            this.messageEditSyncFailed$.next(payload.messageId);
                            return;
                        }
                        const payload = operation.payload as { clientMessageId: string };
                        this.messageSyncFailed$.next(payload.clientMessageId);
                        return;
                    }



                    this.scheduleRetry();
                }
            }
        } finally {
            this.isSyncing = false;
        }
    }


private async syncDeleteMessage(messageId: string): Promise<void> {
  await firstValueFrom(
    this.messagesApi.deleteMessage(messageId)
  );
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

        await this.offlineMessages.deleteByClientMessageId(request.clientMessageId);
        await this.offlineMessages.save(syncedMessage);
    }

    private async syncEditMessage(
        messageId: string,
        request: EditMessageRequest,
        editedAtUtc?: string | null
    ): Promise<void> {
        await firstValueFrom(
            this.messagesApi.editMessage(messageId, request)
        );

        this.messageEditSynced$.next({
            messageId,
            body: request.body,
            editedAtUtc: editedAtUtc ?? new Date().toISOString()
        });
    }


    private scheduleRetry(): void {
        if (this.retryTimer) {
            return;
        }

        this.retryTimer = setTimeout(() => {
            this.retryTimer = null;
            this.requestSync('retry-backoff');
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
        fileIds: string[];
    }): Promise<void> {


        const uploadedAttachments = [];

        for (const fileId of input.fileIds) {


            const localFile = await this.offlineFiles.get(fileId);



            if (!localFile) {
                throw new Error(`Pending file not found: ${fileId}`);
            }

            const uploadFile = new File(
                [localFile.blob],
                localFile.fileName,
                { type: localFile.contentType }
            );



            const uploaded = await firstValueFrom(
                this.filesApi.upload(uploadFile)
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

        await this.offlineMessages.deleteByClientMessageId(input.clientMessageId);

        await this.offlineMessages.save(syncedMessage);

        for (const fileId of input.fileIds) {
            await this.offlineFiles.remove(fileId);
        }

        this.messageSynced$.next(syncedMessage);
    }
}