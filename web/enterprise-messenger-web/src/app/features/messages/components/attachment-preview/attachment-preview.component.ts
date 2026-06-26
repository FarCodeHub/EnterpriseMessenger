import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { MediaCacheService } from '../../../../core/offline/media-cache.service';
import { AttachmentUrlService } from '../../../../core/services/attachment-url.service';
import { MessageAttachmentDto } from '../../models/message-attachment.dto';
import { OfflineFilesRepository } from '../../../../core/offline/offline-files.repository';
import { TranslateModule } from '@ngx-translate/core';
@Component({
  selector: 'app-attachment-preview',
  standalone: true,
   imports: [ TranslateModule],
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
        <img
          class="max-h-[85vh] max-w-[90vw] rounded-xl object-contain"
          [src]="url()"
          [alt]="attachment.fileName"
          (click)="$event.stopPropagation()"
        />
      </div>
    }
  } @else if (attachment.isLocalPending || attachment.localFileId) {
    <div class="flex h-48 w-64 items-center justify-center rounded-xl bg-slate-800 text-xs text-slate-400">
      {{ 'attachments.Preparingimage' | translate }}
    </div>
  } @else if (isOffline()) {
    <div class="flex h-48 w-64 items-center justify-center rounded-xl bg-slate-800 text-center text-xs text-slate-400">
      {{ 'attachments.ImageUnavailableOffline' | translate }}
    </div>
  } @else {
    <div class="flex h-48 w-64 items-center justify-center rounded-xl bg-slate-800 text-xs text-slate-400">
      {{ 'attachments.LoadingImage' | translate }}
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
  private readonly offlineFiles = inject(OfflineFilesRepository);
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
 
 

 
  if (this.attachment.localPreviewUrl) {
    this.url.set(this.attachment.localPreviewUrl);
    return;
  }

if (this.attachment.localFileId) {
  const localFile = await this.offlineFiles.get(this.attachment.localFileId);

  if (localFile) {
    this.url.set(URL.createObjectURL(localFile.blob));
    return;
  }
}

  if (!this.attachment.id) {
    return;
  }

  const cachedUrl = await this.mediaCache.getCachedObjectUrl(this.attachment.id);

  if (cachedUrl) {
    this.url.set(cachedUrl);
    return;
  }

if (this.attachment.isLocalPending) {
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
      this.isOffline.set(true);
      this.hasError.set(true);
    }
  });






    
}



}