import { ConversationType } from './conversation-type';

export interface ConversationListItemDto {
  id: string;
  title: string;
  type: ConversationType;
  lastMessageAtUtc: string | null;
  unreadCount: number;
}