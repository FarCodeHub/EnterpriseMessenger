import { Injectable, signal } from '@angular/core';

interface TypingUser {
  conversationId: string;
  userId: string;
}

@Injectable({
  providedIn: 'root'
})
export class TypingState {
  private readonly typingUsersSignal = signal<TypingUser[]>([]);

  readonly typingUsers = this.typingUsersSignal.asReadonly();

  startTyping(conversationId: string, userId: string): void {
    const current = this.typingUsersSignal();

    const exists = current.some(x =>
      x.conversationId === conversationId &&
      x.userId === userId
    );

    if (exists) {
      return;
    }

    this.typingUsersSignal.set([
      ...current,
      { conversationId, userId }
    ]);
  }

  stopTyping(conversationId: string, userId: string): void {
    this.typingUsersSignal.set(
      this.typingUsersSignal().filter(x =>
        !(x.conversationId === conversationId && x.userId === userId)
      )
    );
  }

  isAnyoneTyping(conversationId: string, currentUserId: string | null | undefined): boolean {
    return this.typingUsersSignal().some(x =>
      x.conversationId === conversationId &&
      x.userId !== currentUserId
    );
  }
}