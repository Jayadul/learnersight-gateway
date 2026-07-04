# NetZeroGateway — Backend (.NET 8)

Multi-channel messaging gateway: one public endpoint (`POST /sendtext`) that parses
channel tags and dispatches to **SMS** (REVE), **WhatsApp** (WaTrend) and **Voice**
(NetZero campaigns), with automatic fallback policies — plus a JWT-secured admin API
consumed by the Angular dashboard.

## Run (development)

```bash
dotnet run --project NetZeroGateway.API     # http://localhost:5212
```

- DB: PostgreSQL on `localhost:5432` (`appsettings.Development.json`). Migrations and
  seed data are applied automatically at startup.
- Swagger (Development only): http://localhost:5212/swagger — two doc groups,
  `gateway` (public SendText) and `admin`.
- Seeded login: `admin` / `Admin@123` (SuperAdmin), `operator` / `Operator@123`.

## Run (Docker)

Use the `docker-compose.yml` at the workspace root (one level up). It runs `db`,
`seq` (log viewer), `api`, `web` and `backup`. API: http://localhost:18081.

## Architecture

Clean Architecture — Application never references Infrastructure; API wires everything.

```
NetZeroGateway.Domain          → Entities, enums (zero dependencies)
NetZeroGateway.Application     → Services, DTOs, interfaces (depends on Domain only)
NetZeroGateway.Infrastructure  → EF Core (snake_case), HTTP clients, strategies, xlsx export
NetZeroGateway.API             → Controllers, middleware, DI wiring (the host)
```

Authorization is convention-driven from `NetZeroGateway.API/authorization.json` —
no `[Authorize]` attributes on controllers.

## The gateway endpoint

`POST /sendtext` — `application/x-www-form-urlencoded`, no auth.
Fields: `toUser` (comma-separated numbers), `messageContent` (channel tags), optional
`callerID`/`apikey`/`secretkey`. Tags: `[sms::…]`, `[wp::…]`, `[smswp::…]`,
`[wpmedia::text||url]`, `[call::campaignId]`; content without tags is sent as plain SMS.
Response: `{"Status":"0","Text":"ACCEPTD","Message_ID":"…"}` (queued) or `101/REJECTD`.

Full third-party documentation lives in the admin UI at **/settings/gateway-simulator**
(SuperAdmin only, with a live simulator), and in `postman/`.

## Backups

- Automatic: the `backup` compose service writes `netzero-db-backup.xlsx` (last 30 days)
  and a restorable `backups/netzero-db-YYYY-MM-DD.dump` (pg_dump, 30-day retention)
  next to the compose file, daily at 00:00 GMT+6.
- On demand: `GET /api/backup/export?from=&to=` (JWT) returns an xlsx for any date range —
  surfaced in the admin UI toolbar (cloud icon).

## Key files

- `NetZeroGateway.API/Extensions/` — all DI registration and pipeline wiring
- `NetZeroGateway.API/appsettings.json` — providers, JWT, seed data (`.Development.json` /
  `.Production.json` hold the connection strings)
- `NetZeroGateway.Infrastructure/Persistence/DataSeeder.cs` — idempotent seeding on startup
