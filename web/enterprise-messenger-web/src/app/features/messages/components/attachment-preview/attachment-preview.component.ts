import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { MediaCacheService } from '../../../../core/offline/media-cache.service';
import { AttachmentUrlService } from '../../../../core/services/attachment-url.service';
import { MessageAttachmentDto } from '../../models/message-attachment.dto';

@Component({
    selector: 'app-attachment-preview',
    standalone: true,
    template: `
@if (isImage()) {
  @if (url()) {


<button
  type="button"
  class="block h-full w-full overflow-hidden rounded-xl"
  (click)="openViewer()"
>
  <img
    class="block w-full object-cover"
    [class.h-full]="compact"
    [class.max-h-80]="!compact"
    [src]="url()"
    [alt]="attachment.fileName"
  />
</button>

    @if (isViewerOpen()) {
      <div
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/90 p-6"
        (click)="closeViewer()"
      >
        <div class="absolute top-4 end-4 flex gap-2" (click)="$event.stopPropagation()">
          <button class="rounded-lg bg-white/10 px-3 py-2 text-white" (click)="zoomOut()">−</button>
          <button class="rounded-lg bg-white/10 px-3 py-2 text-white" (click)="zoomIn()">+</button>
          <button class="rounded-lg bg-white/10 px-3 py-2 text-white" (click)="rotateLeft()">⟲</button>
          <button class="rounded-lg bg-white/10 px-3 py-2 text-white" (click)="rotateRight()">⟳</button>
          <a class="rounded-lg bg-white/10 px-3 py-2 text-white" [href]="url()" target="_blank">Open</a>
          <button class="rounded-lg bg-red-600 px-3 py-2 text-white" (click)="closeViewer()">×</button>
        </div>

        <img
          class="max-h-[85vh] max-w-[90vw] rounded-xl object-contain transition-transform"
          [src]="url()"
          [alt]="attachment.fileName"
          [style.transform]="'scale(' + zoom() + ') rotate(' + rotation() + 'deg)'"
          (click)="$event.stopPropagation()"
        />
      </div>
    }
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
private readonly mediaCache = inject(MediaCacheService);
    @Input({ required: true })
    attachment!: MessageAttachmentDto;
    @Input()
    compact = false;
    readonly url = signal<string | null>(null);
    readonly isViewerOpen = signal(false);
    readonly rotation = signal(0);
    readonly zoom = signal(1);
    readonly isOffline = signal(false);
readonly hasError = signal(false);

    openViewer(): void {
        this.rotation.set(0);
        this.zoom.set(1);
        this.isViewerOpen.set(true);
    }

    closeViewer(): void {
        this.isViewerOpen.set(false);
    }

    rotateRight(): void {
        this.rotation.update(value => value + 90);
    }

    rotateLeft(): void {
        this.rotation.update(value => value - 90);
    }

    zoomIn(): void {
        this.zoom.update(value => Math.min(value + 0.25, 3));
    }

    zoomOut(): void {
        this.zoom.update(value => Math.max(value - 0.25, 0.5));
    }
    isImage(): boolean {
        return this.attachment.contentType.startsWith('image/');
    }
async ngOnInit(): Promise<void> {
  if (!this.attachment.id) {
    return;
  }

  const cachedUrl = await this.mediaCache.getCachedObjectUrl(this.attachment.id);

  if (cachedUrl) {
    this.url.set(cachedUrl);
    return;
  }

  if (!navigator.onLine) {
    this.isOffline.set(true);
    return;
  }

  this.attachmentUrlService.getUrl(this.attachment.id)
    .subscribe({
      next: async signedUrl => {
        try {
          const objectUrl = await this.mediaCache.cacheFromUrl(
            this.attachment.id,
            signedUrl
          );

          this.url.set(objectUrl);
        } catch {
          this.url.set(signedUrl);
        }
      },
      error: () => {
        this.hasError.set(true);
      }
    });
}

}