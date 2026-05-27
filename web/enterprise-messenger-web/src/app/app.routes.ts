import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/login/login.component')
        .then(c => c.LoginComponent)
  },
  {
    path: 'app',
    loadComponent: () =>
      import('./layout/main-layout/main-layout.component')
        .then(c => c.MainLayoutComponent),
    children: [
      {
        path: 'conversations',
        loadComponent: () =>
          import('./features/conversations/conversations-page.component')
            .then(c => c.ConversationsPageComponent)
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'conversations'
      }
    ]
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'auth/login'
  },
  {
    path: '**',
    redirectTo: 'auth/login'
  }
];