# NetZeroGateway — Backend (.NET 8)

Multi-channel messaging gateway: one public endpoint (`POST /sendtext`) that parses
channel tags and dispatches to **SMS** (REVE), **WhatsApp** (WaTrend) and **Voice**
(NetZero campaigns), with automatic fallback — plus a JWT-secured admin API consumed
by the Angular dashboard (sibling repo `../learnersight-netzero-gateway-ui`).

## Run (development)

```bash
dotnet run --project NetZeroGateway.API     # http://localhost:5212
```

- DB: PostgreSQL on `localhost:5432` (`appsettings.Development.json`). Migrations and
  seed data apply automatically at startup.
- Swagger (Development only): http://localhost:5212/swagger — never in production.
- Seeded logins: `admin` / `Admin@123` (SuperAdmin), `operator` / `Operator@123` (Admin).

## Run (Docker)

Use the `docker-compose.yml` at the workspace root (one level up): db, seq, api, web,
backup. API: http://localhost:18081. Details: [docs/deployment.md](docs/deployment.md).

## The gateway endpoint

`POST /sendtext` — form-urlencoded; **requires** `apikey`, `secretkey`, `callerID`
(validated against the active SMS channel config) plus `toUser` and `messageContent`
with channel tags. Response `0/ACCEPTD` or `101/REJECTD`. Full contract:
[docs/api.md](docs/api.md); live simulator in the admin UI (Administration → Gateway
Simulator, SuperAdmin only); Postman collection in `postman/`.

## Documentation

| Doc | Contents |
|---|---|
| [docs/architecture.md](docs/architecture.md) | layers, message flow, job semantics, tags |
| [docs/api.md](docs/api.md) | gateway contract + admin API reference |
| [docs/database.md](docs/database.md) | schema, snake_case, migrations, connections |
| [docs/deployment.md](docs/deployment.md) | compose stack, image publishing, backups, hosting |
| [docs/testing.md](docs/testing.md) | smoke tests, verification |
| [docs/release-process.md](docs/release-process.md) | shipping checklist, rollback |
| [docs/releases/](docs/releases/) | release history — start with 1.0.0.md |
