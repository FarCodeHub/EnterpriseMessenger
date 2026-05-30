export interface SendMessageAttachmentRequest {
   fileId: string;
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