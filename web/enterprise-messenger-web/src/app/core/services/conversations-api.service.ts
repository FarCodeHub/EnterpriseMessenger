import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { BaseApiService } from '../api/base-api.service';

import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';

@Injectable({
  providedIn: 'root'
})
export class ConversationsApiService extends BaseApiService {
  getMyConversations(): Observable<ConversationListItemDto[]> {
    return this.get<ConversationListItemDto[]>('/Conversations');
  }

  getArchivedConversations(): Observable<ConversationListItemDto[]> {
    return this.get<ConversationListItemDto[]>('/Conversations/archived');
  }
}