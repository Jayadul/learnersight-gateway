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
   - Validates `apikey`/`secretkey` against the single **`gateway_credentials`** row
     (AES-decrypted, constant-time comparison), and requires `callerID` to be present.
     Invalid → `101 REJECTD`. Independent of any channel since 2.1 — see
     [adr/0007](adr/0007-decouple-gateway-auth-from-channel-config.md).
   - Parses channel tags from `messageContent`, then checks every label resolves to an
     **active** channel. An unknown or inactive label rejects the request here, before
     anything is queued — dispatch runs after the caller has already been told `ACCEPTD`,
     so a typo caught there would only ever be a warning in Seq.
   - Creates one `MessageRequest` + `DispatchJob`s, returns `0 ACCEPTD` with
     `Message_ID`. Response confirms **queuing, not delivery**.
2. Dispatch (async): `ChannelDispatchService` resolves the channel sender strategy
   (`IChannelSenderFactory`) and calls the provider.
3. On failure: `FallbackHandlerService` retries on the fallback channel — unless the
   request was tagged for more than one channel, in which case fallback is skipped
   entirely. See [Fallback](#fallback) for which policy applies and when.

### Job-creation semantics (frequent confusion — read carefully)

- **SMS and Voice**: ONE job per tag, recipients comma-joined (one batched provider call).
- **WhatsApp (text & media)**: one job PER recipient.
- Dashboard "Channel Breakdown" counts **jobs** (provider calls), not per-recipient
  deliveries. 2 recipients × a message tagged
  `[sms:a::][wp:b::][wp:b::||url][sms:a-wp:b::][call:c::]` = SMS 2 (one job per tag,
  numbers comma-joined), WP 6 (2 each from `wp`, the media `wp` and the WhatsApp leg of
  the combined tag), Voice 1.
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

Fallback only applies to a message that asked for **one** delivery. If the sender's
tags produced more than one `DispatchJob` — `[sms:a::x][wp:b::y]`, any combined
`[sms:a-wp:b::x]` tag, or even two SMS tags — the redundancy was
already paid for, so adding a fallback send would be a third message nobody asked for.
The count is frozen at parse time in `message_requests.is_multi_channel`; counting
sibling jobs at fallback time would give a different answer after a retry or a cleanup.
When the flag is set, **every** job on the request is skipped and the reason is logged
at Warning, so it never looks like a missing policy.

`FallbackHandlerService` skips a policy — and logs the reason at Warning — when:

- it crosses types without `allow_cross_channel` (enforced again at dispatch time,
  so a row edited directly in the database cannot bypass the rule),
- its fallback resolves to the same channel that just failed,
- its fallback channel is inactive, or
- no config resolves for the fallback type at all.

The pacing delay of the fallback channel applies to the retry too, so a burst of
failures cannot outrun the rate limit the delay exists to respect.

## Channel tags

Every tag is `[type:label::content]` — the type keyword picks the channel, the label
picks which gateway of that type carries it:

`[sms:revemain::text]` `[wp:waprimary::text]` `[wp:waprimary::text||url]`
`[sms:revemain-wp:waprimary::text]` `[call:voicemain::campaignId]`
`[call:voicemain::campaignId||lotNumber]`

**The label is mandatory.** Release 2.1 removed the bare `[sms::…]` / `[wp::…]` /
`[call::…]` forms and every `_label:` spelling. Sending **no tag at all** is the only way
to reach a default channel: the whole body then goes as plain SMS on the default SMS
channel. With several channels active per type, an unlabelled tag could only mean
"whichever is default today", which is exactly the ambiguity worth removing from a
contract third parties build against.

Because the type keyword always precedes the label, channel type is read from the tag
rather than inferred from the label — so `label_slug` only has to be unique **within** a
channel type, and no global-uniqueness constraint is needed.

The combined form pairs `sms:` and `wp:` only; `call:` never combines. Parsing splits on
the literal `-wp:`, unambiguous because `sms`/`wp`/`call` are reserved keywords and a slug
is lowercase alphanumeric — no hyphen, no colon.

`||` is the one delimiter across tags: it splits a media URL from its caption and a lot
number from its campaign id. A `call` tag without a lot leaves the lot to the channel
config; a supplied lot overrides it for that message only and is not stored on the
dispatch job. On a combined tag the URL goes to the WhatsApp leg — the SMS leg keeps the
raw body, since dropping the URL there would remove the only copy the recipient could act
on.

WhatsApp has no separate media tag and no separate media channel — the presence of
`||url` is what selects the media sender. An unrecognised tag, an unknown label, or a
label naming an inactive channel **rejects the whole request** with `101 REJECTD` and a
`Reason` naming the problem, rather than being silently dropped: a skipped tag produces
no job and no error, which reads as a successful send that delivered nothing. Rejection
reasons name the replacement for each removed 2.0 form, since a caller hitting one is
mid-migration. Full contract: [api.md](api.md).

## Cross-cutting

- **Auth policies**: convention-driven from `NetZeroGateway.API/authorization.json` —
  never `[Authorize]` attributes. ChannelConfig, FallbackPolicy, AdminUser and
  GatewayCredential require SuperAdmin. `/health` is anonymous.
- **Middleware order**: GlobalException → ApiLogging (masks `apikey`/`secretkey` in
  stored bodies) → routing/CORS/auth.
- **Errors**: GlobalExceptionMiddleware returns ProblemDetails (`title`/`detail`) for
  admin routes, REVE-format `{Status,Text}` for the gateway path.
- **Channel resolution**: several configs per type may be **active**; exactly one is the
  **default** (unique partial index on `(channel_type, is_default)`). Every tag names its
  channel by `label_slug` and resolves against **active** configs only, failing loudly
  when no match exists; the default is reached only by sending no tag at all. Gateway
  authentication is separate from all of this — it reads `gateway_credentials`.
- **Secrets**: channel provider credentials AES-encrypted in `channel_configs.config_json`,
  and the gateway's own keys in `gateway_credentials.api_key_encrypted` /
  `secret_key_encrypted` (`IEncryptionService` for both); rotation via admin UI applies
  instantly. Channel-config reads mask secrets as `***`; the gateway credentials are read
  back decrypted for SuperAdmin, since the pair has to be handed to an integrator and
  nothing else can read the column. Writes treat an omitted, blank or `***` field as
  "leave it alone" so a partial save never blanks a credential.
- **Logging**: Serilog → Seq (compose service, port 18082).
- **Time**: UTC storage/processing everywhere; Dhaka (UTC+6) for display and job
  schedules — see [conventions.md](conventions.md).
- **Background jobs**: Hangfire (same Postgres DB, `hangfire` schema; no dashboard).
  Recurring: `media-cleanup` — Fridays 00:00 server time, deletes expired Media Library
  files and marks them Deleted. Register jobs in Program.cs via `IRecurringJobManager`.
- **Media Library**: authenticated upload (`/api/media`) → local disk
  (`Media:StoragePath`, Docker volume `mediadata`) → public unauthenticated serving at
  `/media/{slug}` for use in outbound messages.
