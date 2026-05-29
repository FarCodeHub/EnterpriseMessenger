import { Component, inject } from '@angular/core';
import { NgClass } from '@angular/common';

import { LanguageService } from '../../../core/i18n/language.service';
import {
  SUPPORTED_LANGUAGES,
  SupportedLanguage
} from '../../../core/i18n/supported-languages';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [NgClass],
  template: `
    <div class="flex items-center gap-2 rounded-xl bg-white/10 p-1">
      @for (language of languages; track language.code) {
        <button
          type="button"
          class="rounded-lg px-3 py-1 text-sm transition"
          [ngClass]="{
            'bg-blue-600 text-white': currentLanguage() === language.code,
            'text-slate-300 hover:bg-white/10': currentLanguage() !== language.code
          }"
          (click)="changeLanguage(language.code)"
        >
          {{ language.label }}
        </button>
      }
    </div>
  `
})
export class LanguageSwitcherComponent {
  private readonly languageService = inject(LanguageService);

  readonly languages = SUPPORTED_LANGUAGES;
  readonly currentLanguage = this.languageService.currentLanguage;

  changeLanguage(language: SupportedLanguage): void {
    this.languageService.use(language);
  }
}