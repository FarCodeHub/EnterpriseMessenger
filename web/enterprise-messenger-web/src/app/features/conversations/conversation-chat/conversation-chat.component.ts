import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-conversation-chat',
  standalone: true,
  template: `
    <div class="flex h-full flex-col bg-slate-950">
      <header class="border-b border-white/10 px-6 py-4">
        <h2 class="text-lg font-bold text-white">
          Conversation
        </h2>
        <p class="text-xs text-slate-400">
          {{ conversationId() }}
        </p>
      </header>

      <div class="flex flex-1 items-center justify-center text-slate-500">
        Messages will be loaded here.
      </div>
    </div>
  `
})
export class ConversationChatComponent {
  private readonly route = inject(ActivatedRoute);

  readonly conversationId = computed(() => {
    return this.route.snapshot.paramMap.get('conversationId');
  });
}