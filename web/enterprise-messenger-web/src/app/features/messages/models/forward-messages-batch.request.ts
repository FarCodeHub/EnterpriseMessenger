export interface ForwardMessagesBatchRequest {
  sourceMessageIds: string[];
  targetConversationIds: string[];
  comment: string | null;
}