import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="min-h-screen bg-slate-950 text-white">
      <header class="flex items-center justify-between border-b border-white/10 px-6 py-4">
        <div class="font-bold">EnterpriseMessenger</div>

        <button
          type="button"
          class="rounded-lg bg-red-600 px-4 py-2 text-sm font-semibold hover:bg-red-500"
          (click)="logout()"
        >
          Logout
        </button>
      </header>

      <main>
        <router-outlet />
      </main>
    </div>
  `
})
export class MainLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

logout(): void {
  this.authService.logout().subscribe({
    next: () => {
      this.router.navigateByUrl('/auth/login');
    }
  });
}
}