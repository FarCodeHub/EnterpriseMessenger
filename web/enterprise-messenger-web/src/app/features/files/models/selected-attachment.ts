export interface SelectedAttachment {
  fileId: string;
  fileName: string;
  contentType: string;
  size: number;
  storagePath: string;
  progress: number;
  isUploading: boolean;
  error: string | null;
  localPreviewUrl?: string;
  localFile?: File;
isOfflinePending?: boolean;
}