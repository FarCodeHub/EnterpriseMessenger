import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

import { MessageDto } from '../../messages/models/message.dto';
import { ConversationListItemDto } from '../models/conversation-list-item.dto';
 

@Component({
  selector: 'app-message-forward-modal',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './message-forward-modal.html'
})
export class MessageForwardModalComponent {
  @Input() open = false;
  @Input() conversations: ConversationListItemDto[] = [];
  @Input() currentConversationId: string | null = null;
  @Input() forwardingMessage: MessageDto | null = null;
  @Input() selectedConversationIds: string[] = [];
  @Input() isForwarding = false;

  @Output() cancel = new EventEmitter<void>();
  @Output() toggleTarget = new EventEmitter<string>();
  @Output() submit = new EventEmitter<void>();

  isSelected(conversationId: string): boolean {
    return this.selectedConversationIds.includes(conversationId);
  }
}