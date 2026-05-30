import { HttpClient, HttpEvent, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { applicationConfig } from '../config/application-config';
import { UploadFileResponse } from '../../features/files/models/upload-file-response';
import { FileDownloadUrlResponse } from '../../features/files/models/file-download-url.response';

@Injectable({
  providedIn: 'root'
})
export class FilesApiService {
  private readonly http = inject(HttpClient);

uploadWithProgress(file: File): Observable<HttpEvent<UploadFileResponse>> {
  const formData = new FormData();
  formData.append('file', file);

  const request = new HttpRequest<FormData>(
    'POST',
    `${applicationConfig.apiBaseUrl}/Files/upload`,
    formData,
    {
      reportProgress: true,
      responseType: 'json'
    }
  );

  return this.http.request<UploadFileResponse>(request);
}

  getDownloadUrl(attachmentId: string): Observable<FileDownloadUrlResponse> {
    return this.http.get<FileDownloadUrlResponse>(
      `${applicationConfig.apiBaseUrl}/Files/${attachmentId}/download-url`
    );
  }
}