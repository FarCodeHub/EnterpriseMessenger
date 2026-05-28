import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { BaseApiService } from '../api/base-api.service';
import { AuthService } from '../auth/auth.service';

import { ConversationListItemDto } from '../../features/conversations/models/conversation-list-item.dto';

@Injectable({
  providedIn: 'root'
})
export class ConversationsApiService extends BaseApiService {
  private readonly authService = inject(AuthService);

  getMyConversations(): Observable<ConversationListItemDto[]> {
    const userId = this.authService.user()?.id;

    if (!userId) {
      throw new Error('User is not authenticated.');
    }

    return this.get<ConversationListItemDto[]>(
      `/Conversations/user/${userId}`
    );
  }

  getArchivedConversations(): Observable<ConversationListItemDto[]> {
    const userId = this.authService.user()?.id;

    if (!userId) {
      throw new Error('User is not authenticated.');
    }

    return this.get<ConversationListItemDto[]>(
      `/Conversations/user/${userId}/archived`
    );
  }
}