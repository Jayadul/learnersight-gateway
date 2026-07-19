---
name: db-migration
description: Add an EF Core migration safely - snake_case naming, idempotent seeding, backup-service impact scan, auto-apply-at-startup implications. Use when changing entities, adding tables or columns, or altering seed data.
---

# DB Migration

Load first: `docs/database.md`.

## Recipe

1. Entity change in **Domain**; EF configuration in **Infrastructure**.
2. `dotnet ef migrations add <Name> -p NetZeroGateway.Infrastructure -s NetZeroGateway.API`
3. **Read the generated migration** — names must come out snake_case
   (EFCore.NamingConventions handles it; odd pluralization → check the DbSet name).
4. Migrations **auto-apply at API startup** — shipping one means the client's DB
   migrates on their next `docker compose up`. Never edit a shipped migration;
   forward-fix with a new one. (`dotnet ef migrations remove` is fine before commit.)
5. Seed changes: `DataSeeder` must stay **idempotent** (check-before-insert).
6. **Backup impact scan** — new/renamed tables or columns:
   - Python backup service (`backup/` at workspace root, **un-versioned** — edit
     carefully) exports via raw snake_case SQL; update its table/column lists.
   - On-demand xlsx export in the admin toolbar may also enumerate tables.
7. Any raw SQL elsewhere must use snake_case names.

## Verify

- `dotnet run` applies it cleanly; confirm in `__ef_migrations_history`.
- If seeding changed, exercise the fresh-database path from `docs/database.md`
  (drop `pgdata`, keep `seqdata`).
- Rollback of a **shipped** migration follows `docs/release-process.md`, not
  `ef migrations remove`.
