export interface MessageAttachmentDto {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
  storagePath: string;
 
localFileId?: string;
localPreviewUrl?: string;
isLocalPending?: boolean;
uploadProgress?: number | null;
}