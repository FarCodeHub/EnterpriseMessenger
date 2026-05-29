import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class PresenceState {
  private readonly onlineUserIdsSignal = signal<Set<string>>(new Set());

  readonly onlineUserIds = this.onlineUserIdsSignal.asReadonly();

  setOnline(userId: string): void {
    const next = new Set(this.onlineUserIdsSignal());
    next.add(userId);
    this.onlineUserIdsSignal.set(next);
  }

  setOffline(userId: string): void {
    const next = new Set(this.onlineUserIdsSignal());
    next.delete(userId);
    this.onlineUserIdsSignal.set(next);
  }

  isOnline(userId: string | null | undefined): boolean {
    if (!userId) {
      return false;
    }

    return this.onlineUserIdsSignal().has(userId);
  }
}