# Testing & Verification

Load this when: verifying changes, writing tests, or smoke-testing the stack.

## Current state (honest)

Backend unit tests exist (`dotnet test`, 50 as of 2.1) covering the tag parser, the
fallback guard, the dispatch lot overlay and the gateway-credential partial update.
There are still **no frontend specs beyond scaffolding and no CI** — the rest of
verification is manual.

## Manual smoke test (the standard loop)

1. `docker compose ps` — all five services up, db healthy.
2. Sign in at http://localhost:18080 (`admin`/`Admin@123`).
3. Gateway round-trip via Administration → Gateway Simulator using the apikey/secretkey
   from **Configuration → Gateway Credentials** (since 2.1 these are the gateway's own
   keys, no longer the SMS channel's). The keys are write-only in the UI, so use the
   values handed over at deployment. Expect `0 ACCEPTD`.
4. Message Logs shows the request; open details → deliveries listed.
5. Wrong credentials in the simulator → `101 REJECTD`.
6. A tag naming a label that does not exist — `[sms:nosuchlabel::test]` — must come back
   `101 REJECTD` with a `Reason` naming the label, **not** `ACCEPTD`. The Gateway Labels
   table on the simulator page lists the labels that do exist.
7. A removed 2.0 tag — `[sms::test]` — must reject with a `Reason` pointing at
   `[sms:yourlabel::…]`.
8. API check without UI:

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

Covered so far: `TemplateParserService` (every 2.1 tag form, the no-tag path, label
validation, and a rejection test per removed 2.0 form), `FallbackHandlerService`, the
`ChannelDispatchService` lot-number overlay, `GatewayCredentialService`'s partial-update
contract, `ChannelSenderFactory` resolution, `ProviderEndpoint` base-URL resolution, and
the `dispatch_job_recipients` rows written at dispatch. Test doubles are hand-rolled in
`Fakes.cs` — no mocking library; members a test does not exercise throw rather than
returning null.

R3.1 adds `RecipientFormat` (every provider's wire format, both directions), the three
new senders, and `ChannelBalanceService` — in particular that a failed refresh keeps the
last known figure rather than blanking it. Provider clients are faked at the interface,
so no test reaches a real vendor.

R3.2 covers the status sync providers — in particular that a rejected *request* leaves
every row untouched while a reported *delivery failure* is written, and that voice
refuses to resolve a row two report records both match — and the resend service's
append-only audit trail and same-type restriction.

The test project references Infrastructure as of R3.0, so sender resolution can be tested
directly. It decides which vendor a message is billed to, which is not something to cover
only through the services that happen to call it.

The tag grammar is a published contract, so treat it as a priority test target: any
change to it needs a test per accepted form **and** per rejected form, since the
rejection text is what an integrator actually reads.

Prefer a unit test over a live dispatch for anything provider-facing: a `[call:…::…]`
send reaches the real voice provider and it will queue a campaign.

## Remaining test targets (Release 2)

- Backend: priority order — SendTextOrchestrator (auth against `gateway_credentials`,
  label validation, job semantics), BackupExport ranges.
- Frontend: `ng test` (Karma/Jasmine already wired); priority — auth interceptor
  refresh flow, validators, ListState.
- Run before every commit that touches the tested area; CI on PR (see release-2-plan.md).
