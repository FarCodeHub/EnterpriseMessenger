import { HttpClient } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';

import { applicationConfig } from '../config/application-config';
import { ChatSignalRService } from '../signalr/chat-signalr.service';

/** Current time in milliseconds; replaceable in tests. */
export const DISPLAY_LATENCY_CLOCK = new InjectionToken<() => number>('DISPLAY_LATENCY_CLOCK', {
  providedIn: 'root',
  factory: () => () => Date.now()
});

/** Runs a callback once the next frame is drawn; replaceable in tests. */
export const DISPLAY_LATENCY_FRAME = new InjectionToken<(callback: () => void) => void>('DISPLAY_LATENCY_FRAME', {
  providedIn: 'root',
  factory: () => callback => {
    requestAnimationFrame(() => callback());
  }
});

/**
 * Measures send-to-display: how long after the server stored a message (its `createdAtUtc`) this client drew it.
 *
 * The client clock is corrected with an offset estimated from the hub's `GetServerTime` (midpoint of the round
 * trip, so the error is at most half a round trip). Samples are sent in batches; they are only measurements, so
 * failures are ignored.
 */
@Injectable({ providedIn: 'root' })
export class DisplayLatencyService {
  static readonly flushIntervalMs = 30_000;
  static readonly clockResyncMs = 10 * 60_000;
  static readonly maxBufferedSamples = 200;

  // Older messages were not delivered live (offline cache, sync backlog); they are not send-to-display samples.
  static readonly maxSampleMs = 60_000;

  private readonly http = inject(HttpClient);
  private readonly chatSignalR = inject(ChatSignalRService);
  private readonly now = inject(DISPLAY_LATENCY_CLOCK);
  private readonly afterNextFrame = inject(DISPLAY_LATENCY_FRAME);

  private clockOffsetMs: number | null = null;
  private clockMeasuredAt = 0;
  private clockSync: Promise<void> | null = null;
  private samples: number[] = [];
  private flushTimer: ReturnType<typeof setInterval> | null = null;

  /** Call when a live message from another user has been added to the open conversation. */
  recordDisplayed(createdAtUtc: string): void {
    if (typeof document !== 'undefined' && document.visibilityState !== 'visible') {
      return;
    }

    const createdAt = Date.parse(createdAtUtc);

    if (Number.isNaN(createdAt)) {
      return;
    }

    void this.ensureClockOffset();

    // The next frame is when the new message is on screen.
    this.afterNextFrame(() => {
      if (this.clockOffsetMs === null) {
        return;
      }

      const latencyMs = this.now() + this.clockOffsetMs - createdAt;

      if (latencyMs > DisplayLatencyService.maxSampleMs || this.samples.length >= DisplayLatencyService.maxBufferedSamples) {
        return;
      }

      this.samples.push(Math.round(latencyMs));
      this.startFlushTimer();
    });
  }

  /** Sends the collected samples now. */
  flush(): void {
    if (this.samples.length === 0) {
      return;
    }

    const samplesMs = this.samples;
    this.samples = [];

    this.http
      .post(`${applicationConfig.apiBaseUrl}/telemetry/display-latency`, { samplesMs })
      .subscribe({ error: () => undefined });
  }

  /** The estimated difference server clock minus this client's clock, once measured. */
  get offsetMs(): number | null {
    return this.clockOffsetMs;
  }

  ensureClockOffset(): Promise<void> {
    if (this.clockSync) {
      return this.clockSync;
    }

    if (this.clockOffsetMs !== null && this.now() - this.clockMeasuredAt < DisplayLatencyService.clockResyncMs) {
      return Promise.resolve();
    }

    this.clockSync = this.measureClockOffset().finally(() => (this.clockSync = null));
    return this.clockSync;
  }

  private async measureClockOffset(): Promise<void> {
    try {
      const sentAt = this.now();
      const serverTime = await this.chatSignalR.getServerTime();
      const receivedAt = this.now();

      const server = serverTime ? Date.parse(serverTime) : Number.NaN;

      if (Number.isNaN(server)) {
        return;
      }

      this.clockOffsetMs = server - (sentAt + receivedAt) / 2;
      this.clockMeasuredAt = receivedAt;
    } catch {
      // Measurements only: try again with the next message.
    }
  }

  private startFlushTimer(): void {
    this.flushTimer ??= setInterval(() => this.flush(), DisplayLatencyService.flushIntervalMs);
  }
}
