# Claude Skills — Layout & Status

Skills are versioned **in the repo they belong to** and discovered automatically by
Claude Code (repo-scoped) — there is no workspace-root copy to sync.

| Location | Skills |
|---|---|
| this repo `.claude/skills/` | backend-endpoint, db-migration, bug-investigation, docs-audit, release-prep, verify-stack |
| ui repo `.claude/skills/` | angular-feature, ui-consistency-review |
| workspace root `.claude/` | settings only (settings.json copy + settings.local.json) — **no skills** |

release-prep and verify-stack span the whole stack but live here because this repo is
the docs/ops hub. If a session hasn't discovered a skill yet, read its `SKILL.md`
directly — skills are plain markdown recipes.

Fresh machine: clone both repos into one workspace folder; copy this repo's
`.claude/settings.json` to `<workspace>/.claude/` (settings don't inherit across
directories; skill paths inside the recipes are workspace-root-relative).

## Proposed for later (create when first needed, not before)

- **security-review** — project checklist seeded from releases/1.0.0.md known
  limitations (the built-in /security-review covers generic diff scanning).
- **release-notes** — turn git log between tags into client-readable notes.
- **test-generation** — once the R2 test harness exists; premature today.

Skipped deliberately: generic "code review", "git workflow", "refactoring" skills —
built-in /code-review, /simplify and docs/git.md already cover them. Custom agents
declined 2026-07-19 (revisit test-engineer once the R2 test harness exists).
