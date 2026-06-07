import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { SendMessageRequest } from '../../features/messages/models/send-message.request';
import { BaseApiService } from '../api/base-api.service';
import { MessageDto } from '../../features/messages/models/message.dto';
import { ForwardMessageRequest } from '../../features/messages/models/forward-message.request';
import { ForwardMessagesBatchRequest } from '../../features/messages/models/forward-messages-batch.request';
@Injectable({
  providedIn: 'root'
})
export class MessagesApiService extends BaseApiService {
  getConversationMessages(conversationId: string, take = 50): Observable<MessageDto[]> {
    return this.get<MessageDto[]>(
      `/Messages/conversation/${conversationId}`,
      { take }
    );
  }
  sendMessage(request: SendMessageRequest): Observable<MessageDto> {
  return this.post<MessageDto, SendMessageRequest>('/messages', request);
}

getConversationMessagesAround(
  conversationId: string,
  sequenceNumber: number,
  radius = 50
): Observable<MessageDto[]> {
  return this.get<MessageDto[]>(
    `/Messages/conversation/${conversationId}/around/${sequenceNumber}?radius=${radius}`
  );
}



forwardMessage(request: ForwardMessageRequest): Observable<MessageDto[]> {
  return this.post<MessageDto[], ForwardMessageRequest>(
    '/Messages/forward',
    request
  );
}

forwardMessagesBatch(
  request: ForwardMessagesBatchRequest
): Observable<MessageDto[]> {
  return this.post<MessageDto[], ForwardMessagesBatchRequest>(
    '/Messages/forward-batch',
    request
  );
}
}