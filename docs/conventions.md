# Cross-Cutting Conventions

Load this when: starting ANY new feature, adding background jobs, handling dates/times
or files, or unsure where a standard lives. Feature-area details stay in their own docs
(see the map at the bottom) — this file holds the standards that apply everywhere.

## Date & time (project-wide standard, established 2026-07)

- **Store and process in UTC, always.** Entities use `DateTimeOffset` +
  `DateTimeOffset.UtcNow` (`BaseEntity` does this); Postgres columns are
  `timestamp with time zone`. Never store local time.
- **Display in Dhaka time (UTC+6).** The business runs in Bangladesh (no DST).
  - Backend: anything human-facing (xlsx reports, formatted strings) converts via
    `AppTime.Resolve(config)` / `AppTime.ToAppTime(...)`
    (`Application/Utilities/AppTime.cs`, config key `App:TimeZone`, default `Asia/Dhaka`).
  - Frontend: `DATE_PIPE_DEFAULT_OPTIONS` in `app.config.ts` sets every `| date` pipe to
    `APP_UTC_OFFSET` from `core/time/app-time.ts`. Manual formatting (charts) must pass
    `timeZone: APP_TIMEZONE`. **Never** add per-usage offsets or rely on the browser zone.
- **User-entered dates** (filter datepickers) are converted to UTC ISO by
  `BaseCrudService.buildParams` before hitting the API — keep that path for new filters.
- API wire format stays UTC (`DateTimeOffset` serialized with offset) — clients convert
  for display, the server never pre-converts API responses.
- **Never hardcode `+6`/offsets anywhere else.** New code resolves through `AppTime`
  (backend) or `core/time/app-time.ts` (frontend).

## Background jobs (Hangfire)

- Storage: same Postgres DB, `hangfire` schema, wired by `AddBackgroundJobs()` in
  `API/Extensions/ServiceCollectionExtensions.cs`. No Hangfire dashboard (same client
  rule spirit as no-Swagger-in-production).
- Register recurring jobs in `Program.cs` via `IRecurringJobManager` (the static
  `RecurringJob` API crashes at startup — storage isn't initialized yet).
- Schedules are business-time: pass
  `new RecurringJobOptions { TimeZone = AppTime.Resolve(app.Configuration) }`.
- Job bodies live on Application-layer service interfaces (e.g.
  `IMediaLibraryService.CleanupExpiredAsync`) so Hangfire serializes an interface call —
  keep jobs idempotent, per-item try/catch (one bad item must not stop the batch), and
  log start/each item/summary.

## File handling (Media Library pattern — reuse for any future file feature)

- Physical storage behind `IMediaStorageService` (local disk implementation:
  `Infrastructure/Services/LocalMediaStorageService.cs`, path from `Media:StoragePath`,
  Docker volume `mediadata:/app/media`). Always resolve paths through the service —
  it guards against path traversal.
- Public names are slugs (`FileNameSlugger`); duplicates get numeric suffixes.
- Deletion = remove the file, keep the row: status `Deleted` + `DeletedAt`/`DeletedBy`
  (`system` for jobs). One shared service method for manual and scheduled deletion.
- The api container runs as non-root `USER app` — any new writable folder must be
  created + chowned in the Dockerfile or volume mounts will be read-only.

## API design

- Thin controllers, kebab-case routes (`api/thing-things`), logic in Application
  services, DTOs never expose entities. Paging params: `page` (1-based) + `pageSize`.
- Auth ONLY via `authorization.json` (convention applies it) — never `[Authorize]`.
- Errors: throw `NotFoundException` / `UnauthorizedException` /
  `InvalidOperationException` with **customer-readable messages**; middleware maps them
  to ProblemDetails and the UI shows `detail` verbatim.
- Enums serialize as integers — document new ones in [api.md](api.md) and mirror them in
  the UI's `core/models/enums.ts`.

## Database

- snake_case automatic (EFCore.NamingConventions); raw SQL must match.
- New entities extend `BaseEntity` (Guid id, UTC CreatedAt/UpdatedAt), get an
  `OnModelCreating` block (max lengths, enum-as-string conversion, indexes) and a
  migration; migrations + idempotent seed auto-apply at startup.

## UI

- Standards live in ui repo `docs/frontend.md` (shared primitives, dialogs, copy rules).
  Non-negotiables recap: reuse `data-table`/`filter-bar`/`ListState`/`BaseCrudService`,
  MatDialog only (480px form / 880px view), `status-badge` for statuses,
  `mat-slide-toggle` not checkboxes, customer language (no dev jargon, no raw enum
  names), toasts via `NotificationService`, deletes confirm with "This cannot be undone."

## Where the other standards live

| Topic | Doc |
|---|---|
| Layering, message flow, auth model | [architecture.md](architecture.md) |
| Endpoint shapes, enum values, gateway contract | [api.md](api.md) |
| Tables, migrations, seed, psql | [database.md](database.md) |
| Docker, images, volumes, backups | [deployment.md](deployment.md) |
| Smoke tests | [testing.md](testing.md) |
| Branching, commits, cross-repo coordination | [git.md](git.md) |
| Shipping / rollback | [release-process.md](release-process.md) |
| Collaboration rules, approval protocol | [claude/workflow.md](claude/workflow.md) |
| Feature recipes (step-by-step) | `.claude/skills/` in each repo — layout in [.claude/skills/README.md](../.claude/skills/README.md) |
