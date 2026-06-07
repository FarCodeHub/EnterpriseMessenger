import {
  ApplicationConfig,
  importProvidersFrom,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection
} from '@angular/core';

import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { TranslateLoader, TranslateModule } from '@ngx-translate/core';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { routes } from './app.routes';
import { HttpTranslateLoader } from './core/i18n/http-translate-loader';
 
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    provideZoneChangeDetection({
      eventCoalescing: true
    }),

    provideRouter(routes),
        provideHttpClient(
      withInterceptors([])
    ),

provideHttpClient(
  withInterceptors([authInterceptor])
),

    importProvidersFrom(
      TranslateModule.forRoot({
        defaultLanguage: 'fa',
        loader: {
          provide: TranslateLoader,
          useClass: HttpTranslateLoader
        }
      })
    )
  ]
};