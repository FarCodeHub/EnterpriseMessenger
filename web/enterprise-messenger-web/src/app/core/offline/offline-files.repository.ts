import { Injectable } from '@angular/core';
import { LocalPendingFile, localDb } from './app-local-db';

@Injectable({
  providedIn: 'root'
})
export class OfflineFilesRepository {
  async save(file: File): Promise<LocalPendingFile> {
    const localFile: LocalPendingFile = {
      id: crypto.randomUUID(),
      fileName: file.name || 'file',
      contentType: file.type || 'application/octet-stream',
      size: file.size,
      blob: file,
      createdAtUtc: new Date().toISOString()
    };

    await localDb().pendingFiles.put(localFile);

    return localFile;
  }

  async get(id: string): Promise<LocalPendingFile | undefined> {
    return localDb().pendingFiles.get(id);
  }

  async remove(id: string): Promise<void> {
    await localDb().pendingFiles.delete(id);
  }
}