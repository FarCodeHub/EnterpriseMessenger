import { MessageAttachmentDto } from './message-attachment.dto';

export interface MessageDto {
  id: string;
  conversationId: string;
  sequenceNumber: number;
  senderUserId: string;
  body: string;
  replyToMessageId: string | null;
  replyToSequenceNumber: number | null;
  createdAtUtc: string;
  editedAtUtc: string | null;
  deletedAtUtc: string | null;
  attachments: MessageAttachmentDto[];
  clientMessageId?: string;
receiptStatus?: 'sending' | 'sent' | 'delivered' | 'seen' | 'failed';
}