import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { applicationConfig } from '../config/application-config';

const ANONYMOUS_PATHS = ['/Auth/login', '/Auth/refresh', '/Auth/config'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Only our own API gets the access token (never the identity provider or other hosts).
  if (!isApiRequest(request) || isAnonymous(request) || request.headers.has('Authorization')) {
    return next(request);
  }

  const token = authService.getAccessToken();

  if (!token) {
    return next(request);
  }

  return next(withToken(request, token)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      // The access token expired: refresh once and retry the request.
      return authService.refresh().pipe(
        switchMap(newToken => next(withToken(request, newToken))),
        catchError((refreshError: unknown) => {
          // Keep the session when the refresh failed only because we are offline.
          if (refreshError instanceof HttpErrorResponse && refreshError.status === 0) {
            return throwError(() => error);
          }

          authService.clearLocalAuth();
          void router.navigateByUrl('/auth/login');

          return throwError(() => error);
        })
      );
    })
  );
};

function withToken(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });
}

function isApiRequest(request: HttpRequest<unknown>): boolean {
  return request.url.startsWith(applicationConfig.apiBaseUrl);
}

function isAnonymous(request: HttpRequest<unknown>): boolean {
  return ANONYMOUS_PATHS.some(path => request.url.includes(path));
}
