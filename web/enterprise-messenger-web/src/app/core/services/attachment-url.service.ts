import { Injectable, inject } from '@angular/core';
import { Observable, map, of, tap } from 'rxjs';

import { FilesApiService } from './files-api.service';

interface CachedAttachmentUrl {
  url: string;
  expiresAt: number;
}

@Injectable({
  providedIn: 'root'
})
export class AttachmentUrlService {
  private readonly filesApi = inject(FilesApiService);

  private readonly cache = new Map<string, CachedAttachmentUrl>();

  getUrl(attachmentId: string): Observable<string> {
    const cached = this.cache.get(attachmentId);

    if (cached && !this.isExpired(cached.expiresAt)) {
      return of(cached.url);
    }

    return this.filesApi.getDownloadUrl(attachmentId).pipe(
tap(response => {
  this.cache.set(attachmentId, {
    url: response.url,
    expiresAt:
      Date.now() +
      response.expiresInSeconds * 1000
  });
}),
      map(response => response.url)
    );
  }

  clear(attachmentId: string): void {
    this.cache.delete(attachmentId);
  }

  clearAll(): void {
    this.cache.clear();
  }

private isExpired(expiresAt: number): boolean {
  return expiresAt - Date.now() < 30_000;
}
}