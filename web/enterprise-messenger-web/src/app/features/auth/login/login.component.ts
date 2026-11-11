import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthConfigService } from '../../../core/auth/auth-config.service';
import { LanguageSwitcherComponent } from '../../../shared/components/language-switcher/language-switcher.component';
@Component({
  selector: 'app-login',
  standalone: true,
imports: [
  CommonModule,
  ReactiveFormsModule,
  TranslateModule,
  LanguageSwitcherComponent
],
  templateUrl: './login.component.html'
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly authConfig = inject(AuthConfigService);
  private readonly router = inject(Router);

  readonly config = this.authConfig.config;
  readonly configFailed = signal(false);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    userName: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    if (this.authService.isAuthenticated()) {
      await this.router.navigateByUrl('/app/conversations');
      return;
    }

    try {
      await this.authConfig.load();
    } catch {
      this.configFailed.set(true);
    }
  }

  async loginWithOrganization(): Promise<void> {
    if (this.isLoading()) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      await this.authService.loginWithOrganization();
    } catch {
      this.errorMessage.set('auth.login.organizationFailed');
      this.isLoading.set(false);
    }
  }

  submit(): void {
    if (this.form.invalid || this.isLoading()) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { userName } = this.form.getRawValue();

    this.authService.login(userName).subscribe({
      next: () => {
        this.router.navigateByUrl('/app/conversations');
      },
      error: () => {
        this.errorMessage.set('auth.login.failed');
        this.isLoading.set(false);
      }
    });
  }
}