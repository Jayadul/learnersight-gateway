# NetZeroGateway — Backend

.NET 8 multi-channel messaging gateway (SMS / WhatsApp / Voice) with automatic
fallback. Clean Architecture: Domain ← Application ← Infrastructure, API hosts all.
Frontend lives in the sibling repo `../learnersight-netzero-gateway-ui`.

Current baseline: **Release 3.0.0** — read `docs/releases/3.0.0.md` before large work,
then `2.1.0.md` and `1.0.0.md` for what came before. Two decisions there will bite a
session that assumes otherwise: a channel type no longer implies its provider
(`docs/adr/0008` — six vendors now, each with its own recipient format and request
shape), and since 2.1 every `/sendtext` tag must name its channel label while the
gateway's own credentials live outside the channel configs (`docs/adr/0007`).

**Fresh machine?** Clone both repos into one workspace folder; docker compose lives
at the workspace root. Skills load from each repo's `.claude/skills/` automatically
(layout + setup: `.claude/skills/README.md` here). This repo's `docs/` is the
documentation hub for the whole product.

## Commands

```bash
dotnet run --project NetZeroGateway.API    # dev API on :5212 (needs local Postgres :5432)
dotnet build                               # use -o <scratch> if VS holds file locks
# full stack: docker compose at the workspace root (one level up)
```

## Load only what the task needs

| Task | Read |
|---|---|
| **Any new feature; dates/times, jobs, files, cross-cutting standards** | `docs/conventions.md` |
| Message flow, channels, fallback, design review | `docs/architecture.md` |
| Endpoints, request/response shapes, gateway contract | `docs/api.md` |
| SQL, migrations, entities, seed data, psql access | `docs/database.md` |
| Docker, images, publishing, backups, hosting | `docs/deployment.md` |
| Verifying changes, smoke tests | `docs/testing.md` |
| Branching, commits, cross-repo coordination | `docs/git.md` |
| Shipping to the client, tagging, rollback | `docs/release-process.md` |
| Collaboration rules, autonomy limits, model policy | `docs/claude/workflow.md` |
| Release history / known debt / lessons | `docs/releases/` |
| Why it's built this way / revisiting a past decision | `docs/adr/` |

Per-layer conventions load automatically from each project's own CLAUDE.md when you
edit files there (API = auth/middleware wiring, Application = services/DTOs,
Infrastructure = EF/providers).

## Non-negotiables (client decisions)

- Never enable Swagger in production. JWT lifetimes stay 15 min access / 7 days refresh.
- Keep it simple and working; ask instead of guessing.
- Authorization is convention-driven from `NetZeroGateway.API/authorization.json` —
  never add `[Authorize]` attributes.
- Database names are snake_case (EFCore.NamingConventions) — external SQL must match.
- **Time**: store/process UTC; display Dhaka (UTC+6) via `AppTime` / `App:TimeZone`;
  background jobs schedule in Dhaka time — full standard in `docs/conventions.md`.
