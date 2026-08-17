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
| `message_requests` | One row per gateway/manual request (filter col: `received_at`) |
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
4 channel configs (one per channel), 2 fallback policies.
Fresh start: `docker compose down`, `docker volume rm netzero-gateway_pgdata` (keep
`seqdata`), `docker compose up -d --build`.

## Backups

See [deployment.md](deployment.md#backups) — nightly xlsx + pg_dump with 30-day retention,
plus on-demand xlsx export from the admin toolbar.
