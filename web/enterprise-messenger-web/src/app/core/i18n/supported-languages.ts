export type SupportedLanguage = 'fa' | 'en' | 'es';

export interface LanguageOption {
  code: SupportedLanguage;
  label: string;
  direction: 'rtl' | 'ltr';
}

export const SUPPORTED_LANGUAGES: LanguageOption[] = [
  { code: 'fa', label: 'فارسی', direction: 'rtl' },
  { code: 'en', label: 'English', direction: 'ltr' },
  { code: 'es', label: 'Español', direction: 'ltr' }
];

export const DEFAULT_LANGUAGE: SupportedLanguage = 'fa';