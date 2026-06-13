import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/i18n/language.service';
import { OfflineSyncService } from './core/offline/offline-sync.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly languageService = inject(LanguageService);
private readonly offlineSync = inject(OfflineSyncService);
  constructor() {
    this.languageService.initialize();
  }
}