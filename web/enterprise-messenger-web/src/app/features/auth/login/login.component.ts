import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/auth/auth.service';
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
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    userName: ['', [Validators.required]]
  });

  submit(): void {
    if (this.form.invalid || this.isLoading()) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
  this.errorMessage.set('auth.login.failed');

    const { userName } = this.form.getRawValue();

    this.authService.login(userName).subscribe({
      next: () => {
        this.router.navigateByUrl('/app/conversations');
      },
      error: () => {
        this.errorMessage.set('Login failed. Please check your username.');
        this.isLoading.set(false);
      }
    });
  }
}