import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';

import { MessageDto } from '../../messages/models/message.dto';

@Component({
  selector: 'app-message-composer',
  standalone: true,
  imports: [FormsModule, TranslateModule],
  templateUrl: './message-composer.html'
})
export class MessageComposerComponent {
  @Input() messageText = '';
  @Input() editingMessage: MessageDto | null = null;
  @Input() replyingToMessage: MessageDto | null = null;

  @Output() messageTextChange = new EventEmitter<string>();
  @Output() sendClicked = new EventEmitter<void>();
  @Output() fileSelected = new EventEmitter<Event>();
  @Output() cancelEditClicked = new EventEmitter<void>();
  @Output() cancelReplyClicked = new EventEmitter<void>();
  @Output() typingChanged = new EventEmitter<void>();

  onTextChange(value: string): void {
    this.messageText = value;
    this.messageTextChange.emit(value);
  }

  onInput(event: Event): void {
    this.resizeTextarea(event);
    this.typingChanged.emit();
  }

  onEnter(event: Event): void {
    const keyboardEvent = event as KeyboardEvent;

    if (!keyboardEvent.shiftKey) {
      keyboardEvent.preventDefault();
      this.sendClicked.emit();
    }
  }

  resizeTextarea(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;

    textarea.style.height = 'auto';
    textarea.style.height = `${textarea.scrollHeight}px`;
  }
}