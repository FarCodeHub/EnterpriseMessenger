import { MessageAttachmentDto } from '../../features/messages/models/message-attachment.dto';

export interface MessageCreatedEvent {
  id: string;
  conversationId: string;
  sequenceNumber: number;
  senderUserId: string;
  clientMessageId: string | null;
  body: string;
  replyToMessageId: string | null;
  replyToSequenceNumber: number | null;
  createdAtUtc: string;
  attachments: MessageAttachmentDto[];
  isDuplicate: boolean;
  forwardedFromMessageId?: string | null;
forwardedFromUserId?: string | null;
forwardedFromDisplayName?: string | null;
  
}

export interface MessageEditedEvent {
  id: string;
  conversationId: string;
  sequenceNumber: number;
  body: string;
  editedAtUtc: string;
}

export interface MessageDeletedEvent {
  id: string;
  conversationId: string;
  deletedAtUtc: string;
}

export interface ConversationUpdatedEvent {
  conversationId: string;
  lastMessageId: string;
  body: string;
  senderUserId: string;
  senderDisplayName: string;
  sequenceNumber: number;
  createdAtUtc: string;

}

export interface TypingEvent {
  conversationId: string;
  userId: string;
}

export interface MessageReceiptEvent {
  conversationId: string;
  userId: string;
  sequenceNumber: number;
}


export interface PresenceSnapshotEvent {
  conversationId: string;
  userIds: string[];
}

export interface PresenceChangedEvent {
  conversationId: string;
  userId: string;
  isOnline: boolean;
}

// export type PresenceEvent = string;
export type ConversationJoinedEvent = string;
export type HubErrorEvent = string;