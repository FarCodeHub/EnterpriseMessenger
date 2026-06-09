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
 

@Injectable({
  providedIn: 'root'
})
export class ChatSignalRService {
  private readonly authService = inject(AuthService);

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

  // readonly presenceRegistered$ = new Subject<PresenceEvent>();

constructor() {
  effect(() => {
    if (!this.networkStatus.isOnline()) {
      void this.disconnect();
    }
  });
}


async connect(): Promise<void> {
  if (!this.networkStatus.isOnline()) {
    return;
  }

  if (this.connection?.state === HubConnectionState.Connected) {
    return;
  }

  const token = this.authService.getAccessToken();

  if (!token) {
    return;
  }

  try {
    this.connection = new HubConnectionBuilder()
      .withUrl(applicationConfig.signalRHubUrl, {
        accessTokenFactory: () => this.authService.getAccessToken() ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.onreconnected(() => {
      this.reconnected$.next();
    });

    this.registerHandlers();

    await this.connection.start();
    await this.registerUser();
  } catch (error) {
    console.warn('SignalR connect skipped/failed:', error);

    try {
      await this.connection?.stop();
    } catch {
      // ignore
    }

    this.connection = null;
  }
}

  async disconnect(): Promise<void> {
    this.stopPresenceRenewTimer();

    if (!this.connection) {
      return;
    }
    await this.connection.stop();
    this.connection = null;
  }

  async registerUser(): Promise<void> {
    if (!this.networkStatus.isOnline()) {
  return;
}
    await this.invoke('RegisterUser');
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