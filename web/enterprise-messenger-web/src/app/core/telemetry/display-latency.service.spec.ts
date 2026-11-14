import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { applicationConfig } from '../config/application-config';
import { ChatSignalRService } from '../signalr/chat-signalr.service';
import { DISPLAY_LATENCY_CLOCK, DISPLAY_LATENCY_FRAME, DisplayLatencyService } from './display-latency.service';

// The clock, the frame and HttpClient are replaced through DI. Using Vitest's `vi` (fake timers, stubGlobal) or
// '@angular/common/http/testing' here broke the whole test run on a clean build ("Vitest failed to find the runner").
describe('DisplayLatencyService', () => {
  const clientNow = Date.parse('2026-11-14T12:00:00.000Z');
  const reportUrl = `${applicationConfig.apiBaseUrl}/telemetry/display-latency`;
  let serverTime: string | null;
  let posts: { url: string; body: unknown }[];
  let service: DisplayLatencyService;

  beforeEach(() => {
    const now = clientNow;
    // The server clock is 2 seconds ahead of this client.
    serverTime = '2026-11-14T12:00:02.0000000Z';
    posts = [];

    TestBed.configureTestingModule({
      providers: [
        { provide: DISPLAY_LATENCY_CLOCK, useValue: () => now },
        // Draw "frames" immediately.
        { provide: DISPLAY_LATENCY_FRAME, useValue: (callback: () => void) => callback() },
        {
          provide: HttpClient,
          useValue: {
            post: (url: string, body: unknown) => {
              posts.push({ url, body });
              return of({});
            }
          }
        },
        { provide: ChatSignalRService, useValue: { getServerTime: () => Promise.resolve(serverTime) } }
      ]
    });

    service = TestBed.inject(DisplayLatencyService);
  });

  it('estimates the clock offset from the server time', async () => {
    await service.ensureClockOffset();

    expect(service.offsetMs).toBe(2_000);
  });

  it('measures against the server clock and sends samples in a batch', async () => {
    await service.ensureClockOffset();

    // Stored by the server 150 ms and 300 ms ago (server clock), shown now.
    service.recordDisplayed('2026-11-14T12:00:01.850Z');
    service.recordDisplayed('2026-11-14T12:00:01.700Z');

    expect(posts).toEqual([]);
    service.flush();

    expect(posts).toEqual([{ url: reportUrl, body: { samplesMs: [150, 300] } }]);
  });

  it('skips messages that were not delivered live and does not send empty batches', async () => {
    await service.ensureClockOffset();

    // Two minutes old: read from the cache or sync, not a live delivery.
    service.recordDisplayed('2026-11-14T11:58:02.000Z');
    service.recordDisplayed('not a date');
    service.flush();

    expect(posts).toEqual([]);
  });

  it('records nothing until the clock offset is known', () => {
    serverTime = null; // not connected

    service.recordDisplayed('2026-11-14T12:00:01.900Z');
    service.flush();

    expect(posts).toEqual([]);
  });
});
