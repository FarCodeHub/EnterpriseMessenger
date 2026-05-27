import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import {
  DEFAULT_LANGUAGE,
  SUPPORTED_LANGUAGES,
  SupportedLanguage
} from './supported-languages';

const LANGUAGE_STORAGE_KEY = 'enterprise_messenger_language';

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);

  private readonly currentLanguageSignal = signal<SupportedLanguage>(
    this.getInitialLanguage()
  );

  readonly currentLanguage = this.currentLanguageSignal.asReadonly();

  initialize(): void {
    const language = this.currentLanguageSignal();

    this.translate.addLangs(SUPPORTED_LANGUAGES.map(x => x.code));
    this.translate.setDefaultLang(DEFAULT_LANGUAGE);
    this.use(language);
  }

  use(language: SupportedLanguage): void {
    const selected = SUPPORTED_LANGUAGES.find(x => x.code === language)
      ?? SUPPORTED_LANGUAGES.find(x => x.code === DEFAULT_LANGUAGE)!;

  this.translate.use(selected.code).subscribe();

    this.currentLanguageSignal.set(selected.code);
    localStorage.setItem(LANGUAGE_STORAGE_KEY, selected.code);

    this.document.documentElement.lang = selected.code;
    this.document.documentElement.dir = selected.direction;
  }

  private getInitialLanguage(): SupportedLanguage {
    const saved = localStorage.getItem(LANGUAGE_STORAGE_KEY) as SupportedLanguage | null;

    if (saved && SUPPORTED_LANGUAGES.some(x => x.code === saved)) {
      return saved;
    }

    return DEFAULT_LANGUAGE;
  }
}