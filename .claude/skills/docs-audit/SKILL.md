---
name: docs-audit
description: Drift-check the docs against the code after a feature lands or before a release - api.md controller map, database.md tables, CLAUDE.md loading tables, skills, ADRs. Use when the user asks to audit, refresh, or sync documentation.
---

# Docs Audit

Goal: surgical drift fixes, not rewrites. Scope from the diff, not from reading
everything.

## Scope

`git log --oneline --stat <last-audited>..HEAD` in both repos → map touched areas to
docs via the backend `CLAUDE.md` loading table. Only open docs the diff implicates.

## Checklist (per implicated doc)

- `docs/api.md` — controller map matches `Controllers/` + `authorization.json`.
- `docs/database.md` — tables list matches entities/migrations; seed data current.
- `docs/architecture.md` — new services, jobs, or flows represented.
- `docs/deployment.md` — compose services/volumes/ports accurate (compose is
  un-versioned: per `docs/git.md`, important knowledge must be mirrored here).
- `docs/conventions.md` — new cross-cutting pattern worth codifying?
- CLAUDE.md ×3 (workspace root + both repos) — loading tables, skills list,
  non-negotiables still true.
- `.claude/skills/` — recipes still match reality; update **both** copies
  (versioned in this repo + live at workspace root).
- `docs/adr/` — was a load-bearing decision made? One-page ADR.
- `docs/releases/` — current plan reflects done/descoped items.

## Output

Two lists: **Changed** (file → one-line what/why) and **Verified current**.
Preserve each doc's existing style and length discipline.

## When not to use

Mid-feature (docs churn), or to write brand-new feature docs — that belongs to the
feature task itself (workflow step "update the one affected doc section").
