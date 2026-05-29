import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-conversations-page',
  standalone: true,
  imports: [TranslateModule],
  template: `
    <div class="flex h-full min-h-0 flex-1 items-center justify-center bg-slate-950">
      <div class="max-w-md text-center">
        <div class="mx-auto mb-6 flex h-20 w-20 items-center justify-center rounded-3xl bg-white/10 text-4xl">
          💬
        </div>

        <h2 class="text-2xl font-bold text-white">
          {{ 'conversations.empty.title' | translate }}
        </h2>

        <p class="mt-3 text-sm leading-6 text-slate-400">
          {{ 'conversations.empty.description' | translate }}
        </p>
      </div>
    </div>
  `
})
export class ConversationsPageComponent {}