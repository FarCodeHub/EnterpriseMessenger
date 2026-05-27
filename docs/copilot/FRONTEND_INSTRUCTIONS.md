# EnterpriseMessenger Frontend Instructions

Frontend stack:

- Angular
- TypeScript
- Tailwind CSS
- RxJS
- SignalR Client
- REST API: `/api/v1`
- Backend base URL in Docker: `http://localhost:8080/api/v1`
- SignalR Hub: `http://localhost:8080/hubs/chat`

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