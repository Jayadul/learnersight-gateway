# MCP Server Recommendations

Honest assessment for this project. Bottom line: **no MCP is required today**.
Consider PostgreSQL later; everything else duplicates tools already in use.

## Team-lead decisions

- **Playwright / browser MCP — DECLINED (2026-07-11).** Do not propose or install.
  UI verification stays manual: the team lead reviews rendered pages; Claude verifies
  via builds + the smoke test in `docs/testing.md`.

## Worth considering later

### PostgreSQL MCP — MEDIUM value
- **Why**: direct schema-aware queries during debugging (data investigations like the
  "why WP_Text=4" question).
- **When**: data investigations, verifying job creation, backup range checks.
- **Tokens**: modest; `docker exec … psql` already works — an MCP is convenience,
  not capability.
- **Security**: point it at localhost:15432 only, read-only role preferred; NEVER at a
  client/production database.

## Skip (and why)

- **GitHub MCP** — `gh` CLI already covers PRs/issues/API with zero extra context cost.
- **Git / Filesystem / Docker MCPs** — native tools already do this; an MCP adds schema
  tokens for nothing.
- **Context7 / docs-search** — Angular 18 and .NET 8 are stable and well within model
  knowledge; occasional WebSearch suffices.
- **Jira / Figma** — the project uses neither; revisit only if the client adopts them.
- **Memory MCP** — Claude Code's built-in file memory + these docs already fill the role.
- **OpenAPI/Swagger MCP** — the API surface is small and documented in api.md; Swagger
  runs in Development anyway.

Rule going forward: add an MCP only when a concrete recurring task is slower without
it — never speculatively (every installed MCP costs schema tokens each session).
