import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { MessageDto } from '../../messages/models/message.dto';
import { AttachmentPreviewComponent } from '../../messages/components/attachment-preview/attachment-preview.component';

@Component({
  selector: 'app-message-bubble',
  standalone: true,
  imports: [
    DatePipe,
    TranslateModule,
    AttachmentPreviewComponent
  ],
  templateUrl: './message-bubble.html',
    host: {
    class: 'block'
  }
})
export class MessageBubbleComponent {
  @Input({ required: true }) message!: MessageDto;
  @Input({ required: true }) currentUserId!: string | null;

  @Input() selectionMode = false;
  @Input() selected = false;
  @Input() repliedMessage: MessageDto | null = null;
  @Input() receiptIcon = '';
  @Input() receiptStatus: 'sent' | 'delivered' | 'seen' | 'failed' | 'sending' | null = null;
  @Output() toggleSelection = new EventEmitter<MessageDto>();
  @Output() openActions = new EventEmitter<{ event: MouseEvent; message: MessageDto }>();
  @Output() retry = new EventEmitter<{ message: MessageDto; event: Event }>();
  @Output() jumpToReply = new EventEmitter<MessageDto>();
  @Output() replyJump = new EventEmitter<MessageDto>();
  isOwnMessage(): boolean {
    return this.message.senderUserId === this.currentUserId;
  }

  onOpenActions(event: MouseEvent): void {
    this.openActions.emit({ event, message: this.message });
  }

  onRetry(event: Event): void {
    this.retry.emit({ message: this.message, event });
  }


  isImageOnlyMessage(): boolean {
    const attachments = this.message.attachments ?? [];

    return (
      attachments.length === 1 &&
      attachments.every(x => x.contentType.startsWith('image/')) &&
      (!this.message.body || this.message.body.trim().length === 0)
    );
  }

  isImageAlbumMessage(): boolean {
    const attachments = this.message.attachments ?? [];

    return (
      attachments.length > 1 &&
      attachments.every(x => x.contentType.startsWith('image/'))
    );
  }

  isMediaOnlyMessage(): boolean {
    const attachments = this.message.attachments ?? [];

    return (
      attachments.length > 0 &&
      attachments.every(x => x.contentType.startsWith('image/')) &&
      (!this.message.body || this.message.body.trim().length === 0)
    );
  }

  isLongTextMessage(): boolean {
    return (this.message.body?.length ?? 0) > 80;
  }

  getTextDirection(text: string | null | undefined): 'rtl' | 'ltr' {
    if (!text) {
      return 'ltr';
    }

    const rtlRegex = /[\u0600-\u06FF]/;
    return rtlRegex.test(text) ? 'rtl' : 'ltr';
  }


}