# Architecture

Load this when: changing message flow, adding a channel, touching fallback logic, or reviewing design.

## System

```
Third-party app ──POST /sendtext──▶ API ──▶ queue (DB) ──▶ dispatch ──▶ SMS (REVE)
Admin UI (Angular) ──/api/*──────▶ API                        ├──▶ WhatsApp (WaTrend)
                                                              └──▶ Voice (NetZero campaigns)
```

One .NET 8 API serves two surfaces: the public gateway endpoint (`/sendtext`) and the
JWT-secured admin API (`/api/*`) consumed by the Angular dashboard.

## Clean Architecture layers

| Project | Holds | Depends on |
|---|---|---|
| `NetZeroGateway.Domain` | Entities, enums | nothing |
| `NetZeroGateway.Application` | Services, DTOs, interfaces | Domain |
| `NetZeroGateway.Infrastructure` | EF Core, HTTP clients, channel strategies, xlsx export | Application (implements it) |
| `NetZeroGateway.API` | Controllers, middleware, DI wiring | everything (the host) |

Rule: Application never references Infrastructure. Each project has its own CLAUDE.md
with layer-specific conventions — those load automatically when you edit files there.

## Message flow (the core path)

1. `POST /sendtext` → `SendTextOrchestratorService`
   - Validates `apikey`/`secretkey`/`callerID` against the **active SMS ChannelConfig**
     (AES-decrypted ConfigJson, constant-time comparison). Invalid → `101 REJECTD`.
   - Parses channel tags from `messageContent`, creates one `MessageRequest` +
     `DispatchJob`s, returns `0 ACCEPTD` with `Message_ID`. Response confirms
     **queuing, not delivery**.
2. Dispatch (async): `ChannelDispatchService` resolves the channel sender strategy
   (`IChannelSenderFactory`) and calls the provider.
3. On failure: `FallbackHandlerService` checks `FallbackPolicy` rules and retries on
   the fallback channel.

### Job-creation semantics (frequent confusion — read carefully)

- **SMS and Voice**: ONE job per tag, recipients comma-joined (one batched provider call).
- **WhatsApp (text & media)**: one job PER recipient.
- Dashboard "Channel Breakdown" counts **jobs** (provider calls), not per-recipient
  deliveries. 2 recipients × all-tags message = SMS 2, WP_Text 4 (2 from wp + 2 from
  smswp), WP_Media 2, Voice 1.

## Channel tags

`[sms::text]` `[wp::text]` `[smswp::text]` `[wpmedia::text||url]` `[call::campaignId]`
— no tag means the whole content goes as plain SMS. Full contract: [api.md](api.md).

## Cross-cutting

- **Auth policies**: convention-driven from `NetZeroGateway.API/authorization.json` —
  never `[Authorize]` attributes. ChannelConfig, FallbackPolicy, AdminUser require SuperAdmin.
- **Middleware order**: GlobalException → ApiLogging (masks `apikey`/`secretkey` in
  stored bodies) → routing/CORS/auth.
- **Errors**: GlobalExceptionMiddleware returns ProblemDetails (`title`/`detail`) for
  admin routes, REVE-format `{Status,Text}` for the gateway path.
- **Secrets**: channel provider credentials AES-encrypted in `channel_configs.config_json`
  (`IEncryptionService`); rotation via admin UI applies instantly.
- **Logging**: Serilog → Seq (compose service, port 18082).
- **Time**: UTC storage/processing everywhere; Dhaka (UTC+6) for display and job
  schedules — see [conventions.md](conventions.md).
- **Background jobs**: Hangfire (same Postgres DB, `hangfire` schema; no dashboard).
  Recurring: `media-cleanup` — Fridays 00:00 server time, deletes expired Media Library
  files and marks them Deleted. Register jobs in Program.cs via `IRecurringJobManager`.
- **Media Library**: authenticated upload (`/api/media`) → local disk
  (`Media:StoragePath`, Docker volume `mediadata`) → public unauthenticated serving at
  `/media/{slug}` for use in outbound messages.
