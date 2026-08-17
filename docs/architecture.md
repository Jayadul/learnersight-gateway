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
   the fallback channel — see [Fallback](#fallback) for which policy applies.

### Job-creation semantics (frequent confusion — read carefully)

- **SMS and Voice**: ONE job per tag, recipients comma-joined (one batched provider call).
- **WhatsApp (text & media)**: one job PER recipient.
- Dashboard "Channel Breakdown" counts **jobs** (provider calls), not per-recipient
  deliveries. 2 recipients × all-tags message = SMS 2, WP 6 (2 from `wp` + 2 from
  `smswp` + 2 from `wpmedia`), Voice 1.
- WhatsApp text and media are **one channel** (`ChannelType.WP`). A job is a media send
  when it carries a `MediaUrl`; `IChannelSenderFactory` keys on ChannelType **plus** that
  flag to pick `WhatsAppTextSender` or `WhatsAppMediaSender`. Both remain separate
  strategies — only how they are selected changed. See
  [adr/0006](adr/0006-unify-whatsapp-channel-type.md).

## Fallback

A `FallbackPolicy` names a trigger channel and a fallback channel, each as a
**type** plus an **optional specific channel** (`trigger_channel_config_id` /
`fallback_channel_config_id`). Null on either side means "any channel of this type"
on the trigger side and "this type's default channel" on the fallback side — the
behaviour that existed before the columns were added.

Fallback stays **within a channel type** by default: WhatsApp retries on another
WhatsApp channel, SMS on another SMS, Voice on another Voice. Crossing types changes
both the delivery format and the per-message cost, so it requires
`allow_cross_channel = true` on the policy; the admin UI raises a warning dialog
before the flag can be turned on.

`FallbackHandlerService` skips a policy — and logs the reason at Warning — when:

- it crosses types without `allow_cross_channel` (enforced again at dispatch time,
  so a row edited directly in the database cannot bypass the rule),
- its fallback resolves to the same channel that just failed,
- its fallback channel is inactive, or
- no config resolves for the fallback type at all.

The pacing delay of the fallback channel applies to the retry too, so a burst of
failures cannot outrun the rate limit the delay exists to respect.

## Channel tags

`[sms::text]` `[wp::text]` `[smswp::text]` `[wpmedia::text||url]`
`[sms_label:slug::text]` `[wp_label:slug::text]` `[call_label:slug::id]`
`[sms_label:a-wp_label:b::text]` `[call::campaignId]`
— no tag means the whole content goes as plain SMS. Full contract: [api.md](api.md).

## Cross-cutting

- **Auth policies**: convention-driven from `NetZeroGateway.API/authorization.json` —
  never `[Authorize]` attributes. ChannelConfig, FallbackPolicy, AdminUser require SuperAdmin.
- **Middleware order**: GlobalException → ApiLogging (masks `apikey`/`secretkey` in
  stored bodies) → routing/CORS/auth.
- **Errors**: GlobalExceptionMiddleware returns ProblemDetails (`title`/`detail`) for
  admin routes, REVE-format `{Status,Text}` for the gateway path.
- **Channel resolution**: several configs per type may be **active**; exactly one is the
  **default** (unique partial index on `(channel_type, is_default)`). Unlabelled tags use
  the default; `_label:slug` tags resolve to an active config by `label_slug` and fail
  loudly when no match exists. The gateway authenticates against the default SMS config.
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
