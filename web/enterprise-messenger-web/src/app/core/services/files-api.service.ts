import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { FileDownloadUrlResponse } from '../../features/files/models/file-download-url.response';
import { BaseApiService } from '../api/base-api.service';
import { UploadFileResponse } from '../../features/files/models/upload-file-response';

@Injectable({
  providedIn: 'root'
})
export class FilesApiService extends BaseApiService {
  upload(file: File): Observable<UploadFileResponse> {
    const formData = new FormData();
    formData.append('file', file);

    return this.post<UploadFileResponse, FormData>(
      '/Files/upload',
      formData
    );
  }


getDownloadUrl(attachmentId: string): Observable<FileDownloadUrlResponse> {
  return this.get<FileDownloadUrlResponse>(
    `/Files/${attachmentId}/download-url`
  );
}


}