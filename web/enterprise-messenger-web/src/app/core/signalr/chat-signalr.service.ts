import { Injectable, effect, inject } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { applicationConfig } from '../config/application-config';
import { NetworkStatusService } from '../offline/network-status.service';
import { ChatSignalREvents } from './chat-signalr-events';
import {
  ConversationJoinedEvent,
  ConversationUpdatedEvent,
  HubErrorEvent,
  MessageCreatedEvent,
  MessageDeletedEvent,
  MessageEditedEvent,
  MessageReceiptEvent,
  PresenceSnapshotEvent,
  PresenceChangedEvent,
  TypingEvent
} from './chat-signalr.models';
import { ConnectionStateService } from '../offline/connection-state.service';
import { OfflineSyncService } from '../offline/offline-sync.service';

@Injectable({
  providedIn: 'root'
})
export class ChatSignalRService {
  private readonly authService = inject(AuthService);
private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
private reconnectDelayMs = 2000;
private readonly maxReconnectDelayMs = 60000;
private isConnecting = false;
  private connection: HubConnection | null = null;
  private presenceRenewTimer: ReturnType<typeof setInterval> | null = null;
  private renewingConversationId: string | null = null;
  readonly messageCreated$ = new Subject<MessageCreatedEvent>();
  readonly messageEdited$ = new Subject<MessageEditedEvent>();
  readonly messageDeleted$ = new Subject<MessageDeletedEvent>();
  readonly reconnected$ = new Subject<void>();
  readonly conversationUpdated$ = new Subject<ConversationUpdatedEvent>();
  private readonly networkStatus = inject(NetworkStatusService);
  readonly typingStarted$ = new Subject<TypingEvent>();
  readonly typingStopped$ = new Subject<TypingEvent>();

  readonly messageSeen$ = new Subject<MessageReceiptEvent>();
  readonly messageDelivered$ = new Subject<MessageReceiptEvent>();

  // readonly presenceOnline$ = new Subject<PresenceEvent>();
  // readonly presenceOffline$ = new Subject<PresenceEvent>();
  readonly presenceSnapshot$ = new Subject<PresenceSnapshotEvent>();
  readonly presenceChanged$ = new Subject<PresenceChangedEvent>();
  readonly conversationJoined$ = new Subject<ConversationJoinedEvent>();
  readonly hubError$ = new Subject<HubErrorEvent>();
  private readonly connectionState = inject(ConnectionStateService);
  private readonly offlineSync = inject(OfflineSyncService);
  // readonly presenceRegistered$ = new Subject<PresenceEvent>();

  constructor() {
    effect(() => {
      if (!this.networkStatus.isOnline()) {
        void this.disconnect();
      }
    });
  }
private scheduleReconnect(reason: string): void {
  if (this.reconnectTimer) {
    return;
  }

 

  this.reconnectTimer = setTimeout(() => {
    this.reconnectTimer = null;
    void this.connect();
  }, this.reconnectDelayMs);

  this.reconnectDelayMs = Math.min(
    this.reconnectDelayMs * 2,
    this.maxReconnectDelayMs
  );
}

private clearReconnect(): void {
  if (this.reconnectTimer) {
    clearTimeout(this.reconnectTimer);
    this.reconnectTimer = null;
  }

  this.reconnectDelayMs = 2000;
}

async connect(): Promise<void> {
 

  if (this.isConnecting) {
    return;
  }

  if (this.connection?.state === HubConnectionState.Connected) {
    return;
  }

  this.isConnecting = true;

  try {
    const token = this.authService.getAccessToken();

    if (!token) {
      this.connectionState.setDisconnected();
      return;
    }

    this.connectionState.setConnecting();

    this.connection = new HubConnectionBuilder()
      .withUrl(applicationConfig.signalRHubUrl, {
        accessTokenFactory: () => this.authService.getAccessToken() ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.onreconnecting(() => {
      this.connectionState.setConnecting();
    });

    this.connection.onreconnected(() => {
      this.clearReconnect();
      this.connectionState.setConnected();
      this.reconnected$.next();
      this.offlineSync.requestSync('signalr-reconnected');
    });

    this.connection.onclose(() => {
      this.connectionState.setDisconnected();
      this.scheduleReconnect('signalr-closed');
    });

    this.registerHandlers();

   

    await this.connection.start();

   

    this.clearReconnect();
    this.connectionState.setConnected();
    this.offlineSync.requestSync('signalr-started');

    await this.registerUser();
  } catch (error) {
   

    this.connection = null;
    this.connectionState.setDisconnected();
    this.scheduleReconnect('connect-failed');
  } finally {
    this.isConnecting = false;
  }
}

async disconnect(): Promise<void> {
  this.clearReconnect();
  this.stopPresenceRenewTimer();

  if (!this.connection) {
    return;
  }

  const connection = this.connection;
  this.connection = null;

  await connection.stop();
  this.connectionState.setDisconnected();
}

  async registerUser(): Promise<void> {
    if (!this.networkStatus.isOnline()) {
      return;
    }
    await this.invoke('RegisterUser');
  }

async reconnectAndSync(reason: string): Promise<void> {
 

  await this.disconnect();

  await this.connect();
}

  async leaveConversation(conversationId: string): Promise<void> {
    if (!this.networkStatus.isOnline()) {
      return;
    }
    await this.invoke('LeaveConversation', conversationId);
  }


  async joinConversation(conversationId: string): Promise<void> {
    if (!this.networkStatus.isOnline()) {
      return;
    }

    await this.connect();

    await this.invoke('JoinConversation', conversationId);
  }

  async startTyping(conversationId: string): Promise<void> {
    if (!this.networkStatus.isOnline()) {
      return;
    }
    await this.invoke('StartTyping', conversationId);
  }

  async stopTyping(conversationId: string): Promise<void> {
    if (!this.networkStatus.isOnline()) {
      return;
    }
    await this.invoke('StopTyping', conversationId);
  }

  async markAsSeen(conversationId: string, sequenceNumber: number): Promise<void> {
    if (!this.networkStatus.isOnline()) {
      return;
    }
    await this.invoke('MarkAsSeen', conversationId, sequenceNumber);
  }

  async markAsDelivered(conversationId: string, sequenceNumber: number): Promise<void> {
    if (!this.networkStatus.isOnline()) {
      return;
    }
    await this.invoke('MarkAsDelivered', conversationId, sequenceNumber);
  }

  /** The server's clock (ISO 8601), or null when not connected. Used to estimate this client's clock offset. */
  async getServerTime(): Promise<string | null> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return null;
    }

    return this.connection.invoke<string>('GetServerTime');
  }

  private async invoke(methodName: string, ...args: unknown[]): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return;
    }
    if (!this.networkStatus.isOnline()) {
      return;
    }
    await this.connection.invoke(methodName, ...args);
  }

  private registerHandlers(): void {



    if (!this.connection) {
      return;
    }





    // this.connection.on(ChatSignalREvents.PresenceRegistered, userId => {
    //   this.presenceRegistered$.next(userId);
    // });


    this.connection.on(ChatSignalREvents.MessageCreated, event => {
      this.messageCreated$.next(event);
    });

    this.connection.on(ChatSignalREvents.MessageEdited, event => {
      this.messageEdited$.next(event);
    });

    this.connection.on(ChatSignalREvents.MessageDeleted, event => {
      this.messageDeleted$.next(event);
    });

    this.connection.on(ChatSignalREvents.ConversationUpdated, event => {
      this.conversationUpdated$.next(event);
    });

    this.connection.on(ChatSignalREvents.TypingStarted, event => {
      this.typingStarted$.next(event);
    });

    this.connection.on(ChatSignalREvents.TypingStopped, event => {
      this.typingStopped$.next(event);
    });

    this.connection.on(ChatSignalREvents.MessageSeen, event => {
      this.messageSeen$.next(event);
    });

    this.connection.on(ChatSignalREvents.MessageDelivered, event => {
      this.messageDelivered$.next(event);
    });

    // this.connection.on(ChatSignalREvents.PresenceOnline, userId => {
    //   this.presenceOnline$.next(userId);
    // });

    // this.connection.on(ChatSignalREvents.PresenceOffline, userId => {
    //   this.presenceOffline$.next(userId);
    // });

    this.connection.on(ChatSignalREvents.PresenceSnapshot, event => {
      this.presenceSnapshot$.next(event);
    });

    this.connection.on(ChatSignalREvents.PresenceChanged, event => {
      this.presenceChanged$.next(event);
    });

    this.connection.on(ChatSignalREvents.ConversationJoined, conversationId => {
      this.conversationJoined$.next(conversationId);
    });

    this.connection.on(ChatSignalREvents.HubError, message => {
      this.hubError$.next(message);
    });
  }


  async renewPresence(conversationId: string): Promise<void> {
    if (!this.networkStatus.isOnline()) {
      return;
    }
    await this.invoke('RenewPresence', conversationId);
  }

  startPresenceRenewTimer(conversationId: string): void {

    if (!this.networkStatus.isOnline()) {
      return;
    }


    this.stopPresenceRenewTimer();

    this.renewingConversationId = conversationId;

    this.presenceRenewTimer = setInterval(() => {
      if (
        this.connection &&
        this.connection.state === HubConnectionState.Connected
      ) {
        if (!this.networkStatus.isOnline()) {
          return;
        }

        if (!this.networkStatus.isOnline()) {
          return;
        }

        this.connection.invoke('RenewPresence', conversationId)
          .catch(error => {
            console.error('RenewPresence failed', error);
          });
      }
    }, 30000);
  }

  stopPresenceRenewTimer(): void {
    if (this.presenceRenewTimer) {
      clearInterval(this.presenceRenewTimer);
      this.presenceRenewTimer = null;
    }

    this.renewingConversationId = null;
  }
}