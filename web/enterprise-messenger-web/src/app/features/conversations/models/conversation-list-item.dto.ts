import { ConversationType } from './conversation-type';

export interface ConversationListItemDto {
  id: string;
  title: string | null;
  displayTitle: string;
  type: ConversationType;

  createdAtUtc: string;
  lastMessageAtUtc: string | null;
  lastMessageId: string | null;
  lastMessagePreview: string | null;
  lastMessageSenderUserId: string | null;
  lastMessageSenderDisplayName: string | null;
   otherParticipantUserId: string | null;
  lastReadSequenceNumber: number;
  unreadCount: number;

  isMuted: boolean;
  isPinned: boolean;
  isArchived: boolean;
}