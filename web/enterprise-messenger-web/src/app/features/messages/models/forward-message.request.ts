export interface ForwardMessageRequest {
  sourceMessageId: string;
  targetConversationIds: string[];
  comment: string | null;
}