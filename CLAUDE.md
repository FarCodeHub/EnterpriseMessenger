# EnterpriseMessenger

An enterprise chat (Telegram-like) for about 10,000 users. Goals: fast and smooth core messaging
(send/receive, search, offline), integration with accounting software and workflow engines, AI features.

The owner writes in Persian; answer in Persian unless asked otherwise. Code, comments and commit
messages are in English.

## Stack

- **Backend:** .NET 10, ASP.NET Core, EF Core + PostgreSQL, SignalR (Redis backplane), MassTransit + RabbitMQ,
  MinIO (files), OpenSearch (full-text search), Serilog.
- **Frontend:** Angular 21 (standalone), Tailwind, RxJS, SignalR client, ngx-translate (fa/en),
  Dexie/IndexedDB (offline), PWA. Frontend rules: `docs/copilot/FRONTEND_INSTRUCTIONS.md`.
- **Auth:** Keycloak (federated with LDAP/AD), Authorization Code + PKCE. The username-only login works only in
  Development/Testing. Details: `docs/keycloak.md`.

## Layout

```text
src/Messenger.Domain          entities, no dependencies
src/Messenger.Application     interfaces + commands/results (use cases)
src/Messenger.Infrastructure  EF Core, implementations, consumers, outbox, search, files, migrations
src/Messenger.Api             controllers, ChatHub, auth setup, background workers
src/Messenger.Contracts       request/response DTOs, integration events, realtime event names
tests/Messenger.ArchitectureTests   layer rules (NetArchTest)
tests/Messenger.IntegrationTests    API tests (WebApplicationFactory)
web/enterprise-messenger-web        Angular app
docs/                               api contract, keycloak, hybrid sync design, frontend roadmap
```

Known debt: `ChatHub`, `MessagesController`, `UsersController` inject `MessengerDbContext` directly
(bypassing Application). Message creation logic is duplicated in `MessageSender` and the two
`MessageForwarder` methods; extract a shared service before touching the send path again.

## Commands

```bash
docker compose up -d                       # postgres, redis, rabbitmq, minio, opensearch, keycloak (dev)
dotnet build EnterpriseMessenger.slnx
dotnet test EnterpriseMessenger.slnx
dotnet ef database update --project src/Messenger.Infrastructure --startup-project src/Messenger.Api
dotnet ef migrations add <Name> --project src/Messenger.Infrastructure --startup-project src/Messenger.Api

cd web/enterprise-messenger-web && npm ci && npm start     # http://localhost:4200
npx ng test --watch=false
```

PostgreSQL concurrency tests (`ConcurrentMessagingTests`) run only when `MESSENGER_TEST_POSTGRES` is set,
e.g. `Host=localhost;Port=5432;Username=postgres;Password=postgres`. CI sets it.

## Git workflow

- Branch from `develop`: `feature/...`, `refactor/...`, `fix/...`, `docs/...`.
- Short lowercase English commit messages, e.g. `lock counters when sending messages`.
- One PR per branch into `develop`; the owner reviews and merges. Release = `develop` → `master`
  with a version commit like `V6-20260626`.
- **Stacked PRs:** before the owner merges one, change the next PR's base to `develop`. (In Nov 2026 a stack
  was merged into its own base branches and two PRs never reached `develop`; fixed by PR #399.)
- Every PR must have green CI (`.github/workflows/ci.yml`): backend build + tests with a PostgreSQL service,
  EF migration drift check (`dotnet ef migrations has-pending-model-changes`), frontend build + tests.
- Never commit secrets. `.env.production` is ignored; `.env.production.example` is the template.

## Rules that the code relies on

- **Sequence numbers:** the conversation counter is locked with `SELECT ... FOR UPDATE`
  (`Persistence/RowLockExtensions.cs`) inside a transaction. User sync counters are locked the same way, always
  in id order (avoids deadlocks). Any new code that numbers messages or sync events must do the same.
- **Idempotency:** messages are unique on (`ConversationId`, `ClientMessageId`); the duplicate check runs after
  taking the lock, and a unique-violation returns the stored message.
- **Side effects go through the outbox** (`IOutboxWriter` → RabbitMQ consumers): search indexing
  (`MessageSearchIndexingConsumer`), notifications. Never call OpenSearch inside the send request.
- **Real-time:** every connection joins `user:{userId}`; message events go to members' user groups through
  `ConversationBroadcaster` (conversation group only above 500 members). Typing/presence/receipts use the
  conversation group. A failed push is logged, never fails the request (clients catch up via sync).
- **Members who left** (`LeftAtUtc != null`) get no deliveries, sync events or pushes.
- **Authorization:** endpoints with `{userId}` in the route must check it is the current user (or Admin).
  Admin-only endpoints use `[Authorize(Roles = "Admin")]`. Attachments are referenced by `fileId` only and must
  belong to the sender (`UploadedFiles` table).
- **Migrations:** create them with `dotnet ef migrations add`; CI fails if the model and the snapshot drift.

## Roadmap

Done (Nov 2026, PRs #392–#399): CI, security hardening, secure attachments, Keycloak login, concurrent-safe
send pipeline, real-time per-user groups.

Next, in order:

1. **Hybrid sync (phase 3)** — design: `docs/hybrid-sync.md` (Persian). Waiting for the owner's answers to its open questions
   (group-size threshold, ~200 by default; "seen by whom" in large groups; event retention, ~90 days).
   Summary: small conversations keep per-user sync events; large ones (> threshold) write one
   `ConversationEvents` row per change with a per-conversation event id, clients pull per conversation and
   detect gaps; receipts move from `MessageDeliveries` to `LastDelivered/LastReadSequenceNumber` on
   `ConversationMember`. Migration in 5 reversible steps (dual write → receipts from counters → new client →
   switch large conversations → clean up). Start with the shared message-creation service.
2. **Local-first client** — render from IndexedDB (`liveQuery`), optimistic send, virtual scroll
   (`CdkVirtualScrollViewport`), sync in a Web Worker.
3. **Search, files, notifications** — Persian analyzer in OpenSearch (normalize ی/ي, ک/ك, ZWNJ), presigned
   direct upload to MinIO, thumbnails/blurhash, Web Push (VAPID).
4. **Load test and observability** — k6/NBomber with 10k connections; OpenTelemetry + Grafana.
   Targets: send-to-display p95 < 200 ms, open chat < 100 ms, sync after a day offline < 2 s.
5. **Integration layer** — bot accounts with API keys, signed webhooks, structured messages
   (`Type` + JSON `Payload`, e.g. invoice approval cards); workflow engine (Elsa Workflows).
6. **AI service** consuming the same outbox events, with per-user access filtering.
