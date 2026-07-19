# Claude Skills — Proposal & Status

Live skills load from the **workspace root** `.claude/skills/` (they span both repos
and the compose stack); the **versioned source of truth** is this repo's
`.claude/skills/`. Edit the versioned copy, then mirror to the root
(see `.claude/skills/README.md`).

## Implemented (v1)

| Skill | Purpose | Load context | Output | Saves |
|---|---|---|---|---|
| `release-prep` | The full ship checklist: build → verify → commit → retag → push images+git | release-process.md | shipped release, digests confirmed | ~3–5k tokens/ship + prevents the stale-image mistake |
| `verify-stack` | Smoke-test the running stack (containers, login, gateway round-trip, roles) | testing.md | pass/fail report | ~2k tokens/verification |
| `ui-consistency-review` | Customer-lens copy/UX audit (badges, dialogs, casing, no jargon/em-dashes) | ui docs/frontend.md | Current/Suggested/Reason findings | ~4k tokens/review — encodes all R1 copy decisions |
| `backend-endpoint` | Add a .NET endpoint the project way (authorization.json, DTOs, DI, snake_case) | architecture.md + api.md | conventional endpoint | ~3k tokens/feature |
| `angular-feature` | Add a UI page over shared primitives (model→service→thin page, dialog pattern) | frontend docs | conventional feature | ~3k tokens/feature + avoids ColumnDefDirective trap |

## Implemented (v2, 2026-07-19)

| Skill | Purpose | Load context | Output | Saves |
|---|---|---|---|---|
| `bug-investigation` | Logs-first triage: Seq → container logs → api_logs/dispatch_jobs → repro → root cause | deployment.md, database.md, testing.md as needed | root cause + minimal fix proposal + impact level | avoids shotgun fixes; ~2–4k tokens/bug |
| `db-migration` | EF migration + snake_case check + backup-service impact scan + shipped-migration rules | database.md | safe migration incl. backup/ updates | prevents the R1 backup-SQL breakage class |
| `docs-audit` | Diff-scoped doc drift check (api map, tables, CLAUDE.md tables, skills, ADRs) | diff + implicated docs only | Changed / Verified-current lists | replaces "re-read all docs" sweeps |

## Proposed for later (create when first needed, not before)

- **security-review** — project-specific checklist seeded from 1.0.0 known limitations
  (the built-in /security-review covers generic diff scanning).
- **release-notes** — turn git log between tags into client-readable notes.
- **test-generation** — once the R2 test harness exists; premature today.

Skipped deliberately: generic "code review", "git workflow", "refactoring" skills —
the built-in /code-review, /simplify and docs/git.md already cover them; duplicating
them as skills would cost tokens, not save them.
