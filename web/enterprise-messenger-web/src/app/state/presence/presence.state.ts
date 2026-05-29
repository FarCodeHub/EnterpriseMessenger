import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class PresenceState {
  private readonly onlineUsersByConversationSignal =
    signal<Record<string, string[]>>({});

  readonly onlineUsersByConversation =
    this.onlineUsersByConversationSignal.asReadonly();

  setSnapshot(conversationId: string, userIds: string[]): void {
    this.onlineUsersByConversationSignal.set({
      ...this.onlineUsersByConversationSignal(),
      [conversationId]: [...new Set(userIds)]
    });
  }

  setUserPresence(
    conversationId: string,
    userId: string,
    isOnline: boolean
  ): void {
    const current = this.onlineUsersByConversationSignal();
    const currentUsers = current[conversationId] ?? [];

    const nextUsers = isOnline
      ? [...new Set([...currentUsers, userId])]
      : currentUsers.filter(x => x !== userId);

    this.onlineUsersByConversationSignal.set({
      ...current,
      [conversationId]: nextUsers
    });
  }

  clearConversation(conversationId: string): void {
    const current = { ...this.onlineUsersByConversationSignal() };

    delete current[conversationId];

    this.onlineUsersByConversationSignal.set(current);
  }

  isOnline(
    conversationId: string | null | undefined,
    userId: string | null | undefined
  ): boolean {
    if (!conversationId || !userId) {
      return false;
    }

    return (this.onlineUsersByConversationSignal()[conversationId] ?? [])
      .includes(userId);
  }
}