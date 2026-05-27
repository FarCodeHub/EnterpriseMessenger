import { HttpClient, HttpParams } from '@angular/common/http';
import { inject } from '@angular/core';
import { appConfig } from '../config/app-config';

export abstract class BaseApiService {
  protected readonly http = inject(HttpClient);
  protected readonly baseUrl = appConfig.apiBaseUrl;

  protected get<TResponse>(url: string, params?: Record<string, string | number | boolean | null | undefined>) {
    return this.http.get<TResponse>(`${this.baseUrl}${url}`, {
      params: this.createParams(params)
    });
  }

  protected post<TResponse, TRequest = unknown>(url: string, body?: TRequest) {
    return this.http.post<TResponse>(`${this.baseUrl}${url}`, body);
  }

  protected put<TResponse, TRequest = unknown>(url: string, body?: TRequest) {
    return this.http.put<TResponse>(`${this.baseUrl}${url}`, body);
  }

  protected patch<TResponse, TRequest = unknown>(url: string, body?: TRequest) {
    return this.http.patch<TResponse>(`${this.baseUrl}${url}`, body);
  }

  protected delete<TResponse>(url: string) {
    return this.http.delete<TResponse>(`${this.baseUrl}${url}`);
  }

  private createParams(params?: Record<string, string | number | boolean | null | undefined>): HttpParams {
    let httpParams = new HttpParams();

    if (!params) {
      return httpParams;
    }

    Object.entries(params).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== '') {
        httpParams = httpParams.set(key, String(value));
      }
    });

    return httpParams;
  }
}