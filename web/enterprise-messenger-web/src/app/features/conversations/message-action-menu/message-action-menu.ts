import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

import { MessageDto } from '../../messages/models/message.dto';

@Component({
  selector: 'app-message-action-menu',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './message-action-menu.html'
})
export class MessageActionMenuComponent {
  @Input() message: MessageDto | null = null;
  @Input() position: { x: number; y: number } | null = null;
  @Input() currentUserId: string | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() reply = new EventEmitter<MessageDto>();
  @Output() forward = new EventEmitter<MessageDto>();
  @Output() select = new EventEmitter<MessageDto>();
  @Output() copy = new EventEmitter<MessageDto>();
  @Output() edit = new EventEmitter<MessageDto>();
  @Output() delete = new EventEmitter<MessageDto>();

  isOwnMessage(): boolean {
    return !!this.message && this.message.senderUserId === this.currentUserId;
  }
}