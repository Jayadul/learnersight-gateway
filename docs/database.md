# Database

Load this when: writing SQL, adding migrations, changing entities, or inspecting data.

## Essentials

- PostgreSQL 16. EF Core + Npgsql with **EFCore.NamingConventions → snake_case**
  (`MessageRequest` → `message_requests`, `IsActive` → `is_active`). Raw SQL and any
  external scripts (e.g. the Python backup service) must use snake_case names.
- Migrations + seed run automatically at API startup (`DataSeeder` is idempotent).
- New migration:
  `dotnet ef migrations add <Name> -p NetZeroGateway.Infrastructure -s NetZeroGateway.API`

## Tables

| Table | Purpose |
|---|---|
| `message_requests` | One row per gateway/manual request (filter col: `received_at`). `is_multi_channel` is set once at parse time when the tags produced more than one job; fallback skips those requests |
| `api_logs` | One row per gateway request. `requested_channels` snapshots the channel types the tags asked for — a snapshot, not an FK, so the row stays readable after a channel is renamed or deleted |
| `dispatch_jobs` | Per-job provider calls (filter col: `created_at`) |
| `api_logs` | Gateway request audit, secrets masked (filter col: `requested_at`) |
| `channel_configs` | Provider credentials, AES-encrypted `config_json`. Many active per type; exactly one `is_default` (unique partial index). `label_slug` is unique per type and addresses a channel from a message tag |
| `fallback_policies` | Trigger→fallback channel rules (`is_global`, `is_active`). Each side is a channel **type** plus an optional `*_channel_config_id` FK naming one channel (null = any/default of that type). `allow_cross_channel` must be true for a rule whose two types differ |
| `admin_users` | Dashboard users, BCrypt password hashes |
| `media_files` | Media Library metadata; unique `stored_file_name`, status Active/Deleted |
| `hangfire.*` | Hangfire background-job bookkeeping (own schema, created on startup) |
| `__ef_migrations_history` | EF bookkeeping (excluded from backups) |

## Connections

| Context | Connection |
|---|---|
| Dev (dotnet run) | `localhost:5432` from `appsettings.Development.json` |
| Docker stack | service `db`; host access `postgresql://postgres:postgres@localhost:15432/NetZeroGatewayDB` |
| psql inside container | `docker exec -it netzero-gateway-db-1 psql -U postgres -d NetZeroGatewayDB` |

## Seed data (fresh database)

2 users (`admin`/`Admin@123` SuperAdmin, `operator`/`Operator@123` Admin),
channel configs from `Seed:ChannelConfigs`, fallback policies from `Seed:FallbackPolicies`.

A seed entry names its channel type as `SMS`, `WP` or `Voice`. The friendly names the
admin UI shows (`WhatsApp`, `Voice Call`) are accepted too, and an unrecognised one logs
an error and skips that entry — seeding runs at boot, so a typo used to stop the whole
gateway starting. A seeded fallback rule crossing channel types needs
`AllowCrossChannel: true`, or it is skipped rather than created in a state where it can
never fire.

**The seed creates; it never re-asserts.** An existing channel config has only its
credentials and provider name refreshed, and an existing fallback policy is left
untouched — so switching a rule off, or renaming a channel, survives a restart. The
flip side: **deleting** a seeded row does not stick. The seeder re-creates it on the next
startup unless the entry is removed from `appsettings*.json` first.

Fresh start: `docker compose down`, `docker volume rm netzero-gateway_pgdata` (keep
`seqdata`), `docker compose up -d --build`.

## Backups

See [deployment.md](deployment.md#backups) — nightly xlsx + pg_dump with 30-day retention,
plus on-demand xlsx export from the admin toolbar.
