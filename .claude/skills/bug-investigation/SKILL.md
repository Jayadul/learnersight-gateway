---
name: bug-investigation
description: Triage a reported bug logs-first - Seq, api_logs, dispatch_jobs, container logs - reproduce, isolate the layer, root-cause, then propose a minimal fix. Use when the user reports broken or incorrect behavior, BEFORE writing any fix.
---

# Bug Investigation

Rule: **root cause before fix**. No shotgun changes. Fix impact level per
`docs/claude/workflow.md` — small bugfix: just do it; anything changing behavior,
schema, or auth: propose first.

## Evidence order (cheapest first)

1. **Structured logs**: Seq (URL/creds in `docs/deployment.md`) — filter by time
   window and exception type.
2. **Container logs**: `docker compose logs api --since 30m` (workspace root);
   `web`/`backup`/`db` as relevant.
3. **Database**: `api_logs` (gateway audit), `message_requests`, `dispatch_jobs`
   (per-provider calls) — connections in `docs/database.md`. Raw SQL is snake_case.
4. **Reproduce**: smoke path in `docs/testing.md`, the Gateway Simulator page
   (SuperAdmin), or direct API calls after `POST /api/auth/login`.

## Isolate the layer

Gateway validation → dispatch strategy → provider call → persistence → UI display.
Name the failing layer before proposing anything.

## Known traps (cost time once — check before deep-diving)

- Backend errors are ProblemDetails: the UI/clients read `detail`, not `message`.
- "Fixed but still broken" → verify the container runs the freshly built image
  (`docker inspect <c> --format {{.Image}}`); compose tags `netzero-gateway-*`.
- Blank table cells in UI → `ColumnDefDirective` missing from component `imports`.
- Enums serialize as **integers** — a "wrong value" may be a mapping issue in the UI.

## Output

Root cause (one sentence) + evidence trail + minimal fix proposal + impact level.
Wait for approval if medium/high. After fixing: re-run the exact repro, then the
`verify-stack` smoke if the stack was rebuilt.
