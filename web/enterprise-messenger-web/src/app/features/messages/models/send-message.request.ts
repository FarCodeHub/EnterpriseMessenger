export interface SendMessageAttachmentRequest {
  fileName: string;
  contentType: string;
  size: number;
  storagePath: string;
}

export interface SendMessageRequest {
  conversationId: string;
  clientMessageId: string;
  body: string;
  replyToMessageId: string | null;
  attachments: SendMessageAttachmentRequest[];
}