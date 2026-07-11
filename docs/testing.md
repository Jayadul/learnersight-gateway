# Testing & Verification

Load this when: verifying changes, writing tests, or smoke-testing the stack.

## Current state (honest)

**There are no automated tests yet** — no backend unit/integration tests, no frontend
specs beyond scaffolding, no CI. This is the #1 technical debt item for Release 2
(see releases/release-2-plan.md). Until then, verification is manual.

## Manual smoke test (the standard loop)

1. `docker compose ps` — all five services up, db healthy.
2. Sign in at http://localhost:18080 (`admin`/`Admin@123`).
3. Gateway round-trip via Administration → Gateway Simulator using the apikey/secretkey
   from Configuration → Channels → SMS (active config). Expect `0 ACCEPTD`.
4. Message Logs shows the request; open details → deliveries listed.
5. Wrong credentials in the simulator → `101 REJECTD`.
6. API check without UI:

```powershell
$login = Invoke-RestMethod -Method Post -Uri "http://localhost:18081/api/auth/login" `
  -ContentType "application/json" -Body '{"username":"admin","password":"Admin@123"}'
Invoke-RestMethod -Uri "http://localhost:18081/api/dashboard/summary" `
  -Headers @{ Authorization = "Bearer $($login.accessToken)" }
```

## Role checks

Sign in as `operator`/`Operator@123` (Admin role): sidebar must show only Monitoring +
Messaging; `/settings/channels` direct URL → Access Denied; ChannelConfig API → 403.

## When tests exist (Release 2 targets)

- Backend: xUnit; priority order — SendTextOrchestrator (tag parsing, auth, job
  semantics), FallbackHandler, BackupExport ranges.
- Frontend: `ng test` (Karma/Jasmine already wired); priority — auth interceptor
  refresh flow, validators, ListState.
- Run before every commit that touches the tested area; CI on PR (see release-2-plan.md).
