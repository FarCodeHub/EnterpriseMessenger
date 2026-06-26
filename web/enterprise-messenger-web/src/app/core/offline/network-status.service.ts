import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class NetworkStatusService {
  readonly isOnline = signal(navigator.onLine);

  constructor() {
 

    window.addEventListener('offline', () => {
      this.isOnline.set(false);
    });
  }
}