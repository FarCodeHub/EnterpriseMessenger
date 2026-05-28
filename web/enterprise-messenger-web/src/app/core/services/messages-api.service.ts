import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { BaseApiService } from '../api/base-api.service';
import { MessageDto } from '../../features/messages/models/message.dto';

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
}