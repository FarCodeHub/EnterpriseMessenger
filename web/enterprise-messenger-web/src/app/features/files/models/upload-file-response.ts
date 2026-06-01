export interface UploadFileResponse {
  fileId: string;
  fileName: string;
  contentType: string;
  size: number;
  storagePath: string;
}