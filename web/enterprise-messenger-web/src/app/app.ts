import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/i18n/language.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly languageService = inject(LanguageService);

  constructor() {
    this.languageService.initialize();
  }
}