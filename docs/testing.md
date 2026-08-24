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

## Unit tests

`NetZeroGateway.Tests` (xUnit, net8.0 — must track the solution TFM). Run with
`dotnet test` from the repo root; run it before every commit that touches a tested area.

Covered so far: `TemplateParserService` (all channel tags incl. `call` campaign/lot
splitting and pre-existing-tag regressions) and the `ChannelDispatchService` lot-number
overlay. Test doubles are hand-rolled in `Fakes.cs` — no mocking library; members a test
does not exercise throw rather than returning null.

Prefer a unit test over a live dispatch for anything provider-facing: a `[call::…]`
send reaches the real voice provider and it will queue a campaign.

## Remaining test targets (Release 2)

- Backend: priority order — SendTextOrchestrator (auth, job semantics),
  FallbackHandler, BackupExport ranges.
- Frontend: `ng test` (Karma/Jasmine already wired); priority — auth interceptor
  refresh flow, validators, ListState.
- Run before every commit that touches the tested area; CI on PR (see release-2-plan.md).
