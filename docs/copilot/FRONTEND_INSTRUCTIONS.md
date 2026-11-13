# EnterpriseMessenger Frontend Instructions

Frontend stack:

- Angular
- TypeScript
- Tailwind CSS
- RxJS
- SignalR Client
- ngx-translate (fa/en, RTL)
- Dexie / IndexedDB for offline data (`core/offline`), PWA service worker
- Sign-in: Keycloak, Authorization Code + PKCE (`core/auth`); the username-only login exists only where the
  API allows it (`GET /api/v1/auth/config`)
- REST API: `/api/v1`
- Backend base URL in development: `http://localhost:8580/api/v1` (the API container of
  `docker-compose.production.yml`; set in `src/environments/environment.ts`). Production builds use `/api/v1`.
- SignalR Hub: `http://localhost:8580/hubs/chat`

## Rules

- Keep frontend inside `/web/enterprise-messenger-web`.
- Use standalone Angular architecture.
- Use feature-based folders.
- Do not call APIs directly from components.
- Use services/facades/stores.
- Do not hardcode backend URLs.
- Use environment files.
- Use JWT Bearer token for protected APIs.
- SignalR must be handled only inside `core/signalr`.
- Components must not create HubConnection directly.
- Use Tailwind for styling.
- Avoid `any`.
- Use strict TypeScript models.
- New offline/sync work follows `docs/roadmap.md` (phase 3: render from IndexedDB with `liveQuery`).
