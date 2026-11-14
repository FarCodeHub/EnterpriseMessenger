# Load test

`Messenger.LoadTest` simulates an organisation: thousands of users connected over SignalR (WebSockets) while some of
them send messages over HTTP. It reports, per conversation type, how long it takes from a sender starting the request
to every other member receiving `message.created`. Sender and receivers run in the same process, so the latencies use
one clock. Results of earlier runs: [`docs/baseline.md`](../docs/baseline.md).

## The synthetic organisation

`seed` writes it straight into a freshly migrated database (creating it through the API would take far too long):

| | per user | members |
| --- | --- | --- |
| private chat | users 2k and 2k+1 | 2 |
| team | users 10b … 10b+9 | 10 |
| team | users 50c … 50c+49 | 50 |
| company channel | everyone; users 0–49 post | all users |
| large groups | users 200d …, 500d …, 1000d … (blocks) | 200, 500, 1000 |

Each simulated user opens (`JoinConversation`) its private chat and its team of 10; `--channel-viewers` of them also
have the channel open. Conversations above 500 members only push live events to members who have them open
(`ConversationBroadcaster`), so the expected deliveries count only those viewers.

Messages: `--channel-share` of them go to the channel and `--large-share` to the large group of `--large-size`
(200, 500 or 1000; every member has it open), the rest 70% private, 20% teams of 10, 10% teams of 50. The cost by
group size measured this way is in `docs/hybrid-sync.md`.
Receipts, typing, presence and sync are not simulated.

## Running it

The API must run the code under test, in Release, from its own folder (the content root is the current directory, so
`appsettings.json` would not be loaded from anywhere else). Use throw-away dependencies so nothing touches a real stack:

```bash
docker run -d --rm --name messenger-load-pg -p 127.0.0.1:55433:5432 -e POSTGRES_PASSWORD=postgres postgres:17 -c max_connections=300
docker run -d --rm --name messenger-load-redis -p 127.0.0.1:56379:6379 redis:7
docker run -d --rm --name messenger-load-rabbit -p 127.0.0.1:55672:5672 rabbitmq:3-management
docker compose up -d prometheus jaeger grafana      # optional: watch the run in Grafana (http://localhost:3000)

PG="Host=127.0.0.1;Port=55433;Database=messenger_load;Username=postgres;Password=postgres"
dotnet ef database update --project src/Messenger.Infrastructure --startup-project src/Messenger.Api --connection "$PG"

dotnet build src/Messenger.Api -c Release
cd src/Messenger.Api/bin/Release/net10.0
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:18080 Auth__AllowLocalLogin=true \
  Jwt__SecretKey=LOAD_TEST_ONLY_SECRET_KEY_0123456789_abcdefghij ConnectionStrings__MessengerDb="$PG" \
  Redis__ConnectionString=127.0.0.1:56379 OpenSearch__Url= \
  RabbitMq__Host=127.0.0.1 RabbitMq__Port=55672 RabbitMq__Username=guest RabbitMq__Password=guest \
  OpenTelemetry__TracesEndpoint=http://127.0.0.1:4318/v1/traces \
  OpenTelemetry__MetricsEndpoint=http://127.0.0.1:9090/api/v1/otlp/v1/metrics \
  dotnet Messenger.Api.dll
```

Then, from `loadtest/Messenger.LoadTest`:

```bash
dotnet run -c Release -- seed --connection "$PG" --users 10000
dotnet run -c Release -- run --api http://127.0.0.1:18080 --secret LOAD_TEST_ONLY_SECRET_KEY_0123456789_abcdefghij \
  --users 10000 --rate 100 --duration 300 --channel-share 0.002
```

The tokens are signed with `--secret`, exactly like `JwtTokenService`, so the test needs no sign-ins (the sign-in
endpoint allows 10 requests a minute per IP). Each run writes a JSON summary to `results/` (not committed).

Seed once per database: messages from earlier runs stay, so repeated runs on the same data see a growing
`Messages`, `MessageDeliveries` and `UserSyncEvents` table.
