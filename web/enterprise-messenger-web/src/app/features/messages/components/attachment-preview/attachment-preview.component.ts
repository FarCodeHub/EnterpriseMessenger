import { Component, Input, OnInit, inject, signal } from '@angular/core';

import { AttachmentUrlService } from '../../../../core/services/attachment-url.service';
import { MessageAttachmentDto } from '../../models/message-attachment.dto';

@Component({
  selector: 'app-attachment-preview',
  standalone: true,
  template: `
    @if (isImage()) {
      @if (url()) {
        <a [href]="url()" target="_blank">
          <img
            class="max-h-64 rounded-xl object-cover"
            [src]="url()"
            [alt]="attachment.fileName"
          />
        </a>
      } @else {
        <div class="rounded-xl bg-black/20 px-3 py-2 text-xs text-slate-300">
          Loading image...
        </div>
      }
    } @else {
      @if (url()) {
        <a
          class="inline-flex items-center gap-2 rounded-lg bg-black/20 px-3 py-2 text-xs hover:bg-black/30"
          [href]="url()"
          target="_blank"
        >
          📎 {{ attachment.fileName }}
        </a>
      } @else {
        <div class="rounded-lg bg-black/20 px-3 py-2 text-xs text-slate-300">
          📎 {{ attachment.fileName }}
        </div>
      }
    }
  `
})
export class AttachmentPreviewComponent implements OnInit {
  private readonly attachmentUrlService = inject(AttachmentUrlService);

  @Input({ required: true })
  attachment!: MessageAttachmentDto;

  readonly url = signal<string | null>(null);

  isImage(): boolean {
    return this.attachment.contentType.startsWith('image/');
  }

  ngOnInit(): void {
    this.attachmentUrlService.getUrl(this.attachment.id)
      .subscribe({
        next: url => {
          this.url.set(url);
        }
      });
  }
}