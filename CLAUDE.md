# EnterpriseMessenger

An enterprise chat (Telegram-like) for about 10,000 users. Goals: fast and smooth core messaging
(send/receive, search, offline), integration with accounting software and workflow engines, AI features.

The owner writes in Persian; answer in Persian unless asked otherwise. Code, comments and commit
messages are in English.

## Stack

- **Backend:** .NET 10, ASP.NET Core, EF Core + PostgreSQL, SignalR (Redis backplane), MassTransit + RabbitMQ,
  MinIO (files), OpenSearch (full-text search, optional), Serilog.
- **Frontend:** Angular 21 (standalone), Tailwind, RxJS, SignalR client, ngx-translate (fa/en),
  Dexie/IndexedDB (offline), PWA. Frontend rules: `docs/copilot/FRONTEND_INSTRUCTIONS.md`.
- **Auth:** Keycloak (federated with LDAP/AD), Authorization Code + PKCE. The username-only login is on in
  Development/Testing; elsewhere only with `Auth__AllowLocalLogin=true`, which is insecure (anyone can sign in as
  anyone) and logs a warning at startup. Details: `docs/keycloak.md`.
- **Search:** with `OpenSearch:Url` set, OpenSearch indexes and searches messages. Empty, a no-op indexer and a
  PostgreSQL substring search are used instead (dev and small installs; no index, slow on large data).
- **API reference:** Swagger UI in Development (`http://localhost:5113/swagger`). `docs/api-contract.md` is old.
- **Observability:** OpenTelemetry, on only when `OpenTelemetry:TracesEndpoint` / `MetricsEndpoint` are set
  (`Observability/ObservabilitySetup.cs`). Traces → Jaeger (`:16686`), metrics → Prometheus 3 over OTLP (`:9090`),
  Grafana (`:3000`) with the provisioned "Messenger overview" dashboard (`observability/`). The messenger's own
  metrics are in `Infrastructure/Observability/MessengerMetrics.cs`; histograms in seconds need explicit bucket
  advice (the OpenTelemetry default buckets are meant for milliseconds).
- **Send-to-display:** the web client (`core/telemetry/display-latency.service.ts`) estimates its clock offset with
  the hub's `GetServerTime`, measures `createdAtUtc` → next frame for live messages from others, and posts batches
  to `POST /api/v1/telemetry/display-latency` every 30 s → `messenger.message.display.latency`.

## Layout

```text
src/Messenger.Domain          entities, no dependencies
src/Messenger.Application     interfaces + commands/results (use cases)
src/Messenger.Infrastructure  EF Core, implementations, consumers, outbox, search, files, migrations
src/Messenger.Api             controllers, ChatHub, auth setup, background workers
src/Messenger.Contracts       request/response DTOs, integration events, realtime event names
tests/Messenger.ArchitectureTests   layer rules (NetArchTest)
tests/Messenger.IntegrationTests    API tests (WebApplicationFactory)
web/enterprise-messenger-web        Angular app: src/app/{core (api, auth, signalr, offline), features/*,
                                    shared, state, layout}
keycloak/realm-messenger.json       realm imported on first start, then managed in the admin console
docker-compose.yml                  dev dependencies (project enterprise-messenger-dev)
docker-compose.production.yml       full stack (project enterprisemessenger): API, web, nginx, Keycloak, ...
observability/                      Prometheus config, Grafana data sources and dashboards
loadtest/                           load generator: 10k SignalR users + senders (see loadtest/README.md)
docs/                               roadmap, hybrid sync design, keycloak, api contract (old)
```

Known debt:
- 10 Api types still use `MessengerDbContext` directly: 3 read-only controllers (audit logs, dead letters,
  conversation search) and the 7 background workers (they belong in Infrastructure). The list is in
  `tests/Messenger.ArchitectureTests/ApiDbContextTests.cs`; new ones fail the test, fixed ones must be removed.
  Write paths (messages, conversations, users, auth, `ChatHub`, `ConversationBroadcaster`) are clean.
- `GET /api/v1/users` returns every user, without paging.
- Receipts: only `MarkAsDelivered`/`MarkAsSeen` (per sequence number) remain on the hub; the per-message
  `MessageDeliveries` rows are still written on send and go away with hybrid sync (phase 4).

## Commands

```bash
docker compose up -d                       # postgres, redis, rabbitmq, minio, opensearch, keycloak,
                                           # prometheus, jaeger, grafana (dev)
dotnet build EnterpriseMessenger.slnx
dotnet test EnterpriseMessenger.slnx
dotnet ef database update --project src/Messenger.Infrastructure --startup-project src/Messenger.Api
dotnet ef migrations add <Name> --project src/Messenger.Infrastructure --startup-project src/Messenger.Api

cd web/enterprise-messenger-web && npm ci && npm start     # http://localhost:4200
npx ng test --watch=false
```

On Windows, `ng test` right after clearing `.angular/cache` sometimes fails every spec with "Vitest failed to find
the runner" (seen in Nov 2026 when adding a spec); the next run, with the cache warm, passes. CI (Linux, always
clean) is the reference.

The dev and production compose files are separate projects with their own containers and volumes, but they
publish some of the same host ports: stop one stack before starting the other. The owner often runs the
production stack locally (PostgreSQL 15 on 127.0.0.1:15432, API on :8580).

PostgreSQL tests (`[PostgresFact]`) run only when `MESSENGER_TEST_POSTGRES` is set; CI sets it. Locally, use a
throw-away server instead of a compose database:

```bash
docker run -d --rm --name messenger-test-pg -e POSTGRES_PASSWORD=postgres -p 127.0.0.1:55432:5432 postgres:17
MESSENGER_TEST_POSTGRES="Host=127.0.0.1;Port=55432;Username=postgres;Password=postgres" dotnet test EnterpriseMessenger.slnx
docker stop messenger-test-pg
```

`dotnet run` uses `launchSettings.json` (Development); add `--no-launch-profile` to run with another environment.

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

- **One place creates messages:** `Messages/MessageCreator.cs` (used by `MessageSender` and `MessageForwarder`)
  numbers the message and writes its attachments, deliveries, sync events, audit entry and outbox event. New ways
  of creating messages (bots, integrations) must use it: `LockCountersAsync` → `PrepareTargetsAsync` →
  `CreateAsync`, inside the caller's transaction.
- **Sequence numbers:** the conversation counter is locked with `SELECT ... FOR UPDATE`
  (`Persistence/RowLockExtensions.cs`) inside a transaction. User sync counters are locked the same way, always
  in id order (avoids deadlocks). Any new code that numbers messages or sync events must do the same.
- **Idempotency:** messages are unique on (`ConversationId`, `ClientMessageId`); the duplicate check runs after
  taking the lock, and a unique-violation returns the stored message.
- **Side effects go through the outbox** (`IOutboxWriter` → RabbitMQ consumers): search indexing
  (`MessageSearchIndexingConsumer`, for created/edited/deleted messages), notifications. Never call OpenSearch
  inside a request. Consumers re-read the current state from the database, so late, duplicate or out-of-order
  events are harmless. Integration events must have a namespace (MassTransit rejects them otherwise;
  `IntegrationEventTests` publishes every one). `OutboxPublisher` claims events with `FOR UPDATE SKIP LOCKED`
  (several API instances never publish one twice); settings `Outbox:BatchSize` (200), `Outbox:PublishConcurrency`
  (32), `Outbox:PollIntervalMilliseconds` (1000, only when caught up).
- **Optional services stay optional:** without OpenSearch, a no-op indexer and PostgreSQL search are used; without
  Redis, in-memory presence and connection tracking (one API instance only). Anything that depends on them must
  work with the fallback. `SearchDisabledTests` and `RedisDisabledTests` start the app without them and validate
  every DI registration.
- **Real-time:** every connection joins `user:{userId}`; message events go to members' user groups through
  `ConversationBroadcaster` (conversation group only above 500 members). Typing/presence/receipts use the
  conversation group. A failed push is logged, never fails the request (clients catch up via sync).
- **Members who left** (`LeftAtUtc != null`) get no deliveries, sync events or pushes.
- **Authorization:** endpoints with `{userId}` in the route must check it is the current user (or Admin).
  Admin-only endpoints use `[Authorize(Roles = "Admin")]`. Attachments are referenced by `fileId` only and must
  belong to the sender (`UploadedFiles` table).
- **Migrations:** create them with `dotnet ef migrations add`; CI fails if the model and the snapshot drift.

## Roadmap

Full plan with steps, PRs and exit criteria: `docs/roadmap.md` (Persian). Work one step per branch/PR, in
order; update that file (and this section if needed) when a step is merged. Announce each finished step.

Done: CI, security hardening, secure attachments, Keycloak login, concurrent-safe send pipeline, real-time
per-user groups (PRs #392–#399); phase 0 stabilisation (PRs #402–#406).

Phases:
0. Stabilisation — done.
1. Send pipeline: shared message-creation service; edit/delete indexing through the outbox; DbContext out of
   the write paths — done.
2. **In progress** (2.1 OpenTelemetry + Grafana, 2.2 send-to-display, 2.3 load test done; baseline and findings
   in `docs/baseline.md`: one 10k-member channel message locks every member's sync counter for ~1.3 s and pushes
   p95 of all messages to ~1.1 s; 2.5 raised outbox publishing from 10 to 80+ events/s). Measure first: OpenTelemetry + Grafana, client send-to-display metric, k6/NBomber with 10k connections,
   real group-size stats (they answer the hybrid sync threshold question).
3. Local-first client: render from IndexedDB (`liveQuery`, `[conversationId+sequenceNumber]` index, paged
   window), virtual scroll, sync in a Worker with one leader tab, gap detection. Today the client is
   cache-then-network: signals are the source of truth and a chat loads all its cached messages with `toArray()`.
4. Hybrid sync — design `docs/hybrid-sync.md`; open questions: group-size threshold (~200), "seen by whom" in
   large groups, event retention (~90 days).
5. Local search (MiniSearch in a Worker), ZWNJ in the OpenSearch analyzer (ی/ي, ک/ك are already normalized),
   presigned multipart upload, thumbnails + tiny inline preview, Web Push (VAPID).
6. Integration layer: bot accounts, signed webhooks, structured messages with callbacks, accounting adapters as
   separate services, the organisation's workflow engine (Elsa only if there is none).
7. AI: embeddings from outbox events, k-NN with membership-period access filter, pluggable model (on-prem or cloud).
8. Production readiness: PostgreSQL HA and tested backups, PgBouncer, final load test, security review.

Targets: send-to-display p95 < 200 ms, open chat < 100 ms, sync after a day offline < 2 s.
