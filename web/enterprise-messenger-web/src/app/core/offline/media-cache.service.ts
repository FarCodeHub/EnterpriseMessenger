import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class MediaCacheService {
  private readonly cacheName = 'enterprise-messenger-media-cache-v1';

  async getCachedObjectUrl(attachmentId: string): Promise<string | null> {
    const cache = await caches.open(this.cacheName);
    const response = await cache.match(this.getCacheKey(attachmentId));

    if (!response) {
      return null;
    }

    const blob = await response.blob();

    return URL.createObjectURL(blob);
  }

  async cacheFromUrl(
    attachmentId: string,
    url: string
  ): Promise<string> {
    const response = await fetch(url);

    if (!response.ok) {
      throw new Error('Failed to fetch media for cache.');
    }

    const blob = await response.blob();

    const cache = await caches.open(this.cacheName);

    await cache.put(
      this.getCacheKey(attachmentId),
      new Response(blob, {
        headers: {
          'Content-Type': blob.type || 'application/octet-stream'
        }
      })
    );

    return URL.createObjectURL(blob);
  }

  async remove(attachmentId: string): Promise<void> {
    const cache = await caches.open(this.cacheName);
    await cache.delete(this.getCacheKey(attachmentId));
  }

  private getCacheKey(attachmentId: string): string {
    return `/media-cache/${attachmentId}`;
  }
}