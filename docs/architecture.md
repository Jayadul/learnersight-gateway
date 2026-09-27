# Architecture

Load this when: changing message flow, adding a channel, touching fallback logic, or reviewing design.

## System

```
Third-party app ──POST/GET /sendtext──▶ API ──▶ queue (DB) ──▶ dispatch ──▶ SMS (REVE)
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

1. `POST /sendtext` (or `GET /sendtext` — same fields as query params, same call, R3.3)
   → `SendTextOrchestratorService`
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
   (`IChannelSenderFactory`, keyed on channel type **plus provider** since R3.0) and calls
   the provider, then writes one `dispatch_job_recipients` row per recipient.
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
- **Every job also writes `dispatch_job_recipients`** — one row per number, so a batched
  SMS or Voice job can record which recipients actually arrived. Job status is what the
  provider said at hand-off; recipient status is what happened afterwards, and the two
  can legitimately disagree. Writing these rows never fails a send: the message has
  already gone out by then, so an error is logged and swallowed.

## Providers (R3.0)

A channel type no longer implies its vendor. `ChannelConfig.Provider` names it, and
`IChannelSenderFactory` resolves on `(ChannelType, Provider, HandlesMedia)` — a channel
naming a provider with no registered sender throws rather than substituting another
vendor, because substituting would bill the wrong account. Duplicate registrations fail
at startup instead of resolving by DI order.

Each channel may carry its own `baseUrl` in `config_json`; blank falls back to
`Providers:*:BaseUrl`. Before R3.0 the base URL was bound once per channel type at DI
registration, which is what made one-provider-per-type structural. See
[adr/0008](adr/0008-multi-provider-channels.md).

Only providers with a registered sender are selectable
(`ChannelConfigService.ProvidersFor`): **SMS** — REVE, Chithi; **WhatsApp** —
WaTrend, NetZero Inbox (v1), NetZero Inbox (QR); **Voice** — NetZero Voice.

**NetZero Inbox is two providers, not one.** The vendor exposes two unrelated WhatsApp
APIs on one host, sharing only the token:

| | `NetZeroInbox` (v1) | `NetZeroInboxQr` (QR device) |
|---|---|---|
| Request | `POST /api/v1/send-message`, JSON body | `GET /api/qr/rest/send_message`, query string |
| Scheme | `https` | `http` |
| Sender | none | `from` — the linked device's number |
| Recipient | `+8801700000001` | `8801700000001` |
| Media | none (undocumented, refused) | image only (`messageType=image&imageUrl=…&caption=…`), confirmed working |

Sending either recipient form to the other API is a wrong number, which is why they are
separate `Provider` members rather than a mode flag: the format is resolved from the
provider alone, and a mode would put that decision out of `RecipientFormat`'s reach.
v1 has no documented media contract at all and refuses a media send rather than guess a
request shape the vendor has not described. The QR API does support media — a `wp` tag
with `||mediaUrl` sends `messageType=image` when the URL's extension is a known image
type (jpg, jpeg, png, gif, webp, bmp); any other extension is refused rather than risk a
guessed `messageType` on an unconfirmed file type (R3.3).

**Hotfix batch 6 changed the modelling below:** a template send is now a **dedicated channel**
with its own provider, `Provider.NetZeroInboxTemplate` (7), seeded as "WA Template" (label
`watemplate`) and carrying only `templateToken` and `baseUrl`. The token used to be an extra
field on the regular v1 channel, so nothing in the channel list or the Send Template dropdown
was named Template. `ManualSendService` accepts a template send only on that provider;
`hasTemplateToken` is true only for it, and `isTemplateOnly` lets ordinary send pickers, fallback
rule pickers and the Simulator label table leave it out. It registers a `NetZeroInboxTemplateOnlySender`
in the dispatch pipeline whose only job is to fail a text or media tag with a plain reason, it
cannot be the default channel, and Meta's own refusals (for example error 131030, recipient not on
the WhatsApp Business test list) are surfaced instead of a bare `unknown_error`. The v1 and QR
channels are unchanged. What follows describes the original design; where it says "not a new
Provider member" or "a field on the v1 channel", read it with this note.

**WhatsApp Template send (R3.4)** is a NetZero Inbox (v1)-specific capability, not a new
`Provider` enum member and not reachable through `/sendtext`'s tag grammar — a template
(name + ordered variables, optional media) is a structured payload that does not fit a
free-text tag, and does not need fallback/retry semantics. It bypasses
`IChannelSenderFactory` entirely (that resolves on `(ChannelType, Provider, HandlesMedia)`,
which has no slot for "template vs freeform text") via its own
`IWhatsAppTemplateSender` interface, called directly from `ManualSendService` —
admin-triggered and synchronous, closer to `IChannelBalanceProvider` than to a dispatch
sender. Recipients still convert to the vendor's `+8801700000001` shape via the same
`RecipientFormat.ToPlus` the regular v1 send already uses. The channel's config carries a
separate `templateToken` field, distinct from the regular send `token` — masked the same
way (R3.4's partial-mask rule matches on the field-name shape, not an exact list) — and
`GET /api/manual-send/channels` reports whether a channel has one via `hasTemplateToken`,
without exposing the token itself.

**Verified against the real vendor in hotfix batch 4** — batch 3 shipped this untested
against the actual API. Two real gaps surfaced: the vendor's query-string duplicates of
`Content-Type`/`Authorization` (first assumed to be Postman export noise) turned out to
be required, and `enableLog` (already sent by the regular text send) was missing here too
— without both, every response field came back null. Once the request matched, a second
real gap appeared: a genuinely successful, delivered send still read as "failed", because
the real response shape is `{"success":true,"metaResponse":{"messages":[{"id":"wamid...",
"message_status":"accepted"}]}}` — a boolean `success` and the message id inside a
`messages` array — not the flat `status`/`metaResponse.message_id` shape the vendor's own
documentation implied and batch 3 modeled from it.

### Recipient formats differ per provider (R3.1)

Numbers are stored canonically: digits, country code, no plus (`8801700000001`). Two
providers disagree, in opposite directions, so `RecipientFormat` converts per provider
rather than per vendor platform:

| Provider | On the wire | Note |
|---|---|---|
| REVE, WaTrend, NetZero Voice, **Chithi**, **NetZero Inbox QR** | `8801700000001` | the stored form |
| NetZero Inbox (v1) | `+8801700000001` | WhatsApp Cloud API shape |

GenNet (SMS, `api_token`/`sid`, bulk `/send-sms/bulk`, local-form recipients) was retired
in a later release once it turned out to be the same vendor account as Chithi — see
`docs/releases/` for when. Chithi kept its own implementation (one `msisdn` per call at
`/send-sms`, stored-form recipients) rather than inheriting GenNet's, since shared
credentials said nothing about the wire format.

The country code is `880`, not `88`. `8801748848039` is `880` + the subscriber number,
so the local form is `0` + that. Treating it as `88` leaves two leading zeros — a
different number.

### Delivery status sync (R3.2)

A third provider capability, independent of the other two. REVE, Chithi and NetZero
Voice answer what became of a message. A channel type never implies a sync exists.

**WhatsApp resolves at send time (hotfix batch 6).** No WhatsApp provider (WaTrend, Inbox v1,
Inbox QR, Templates) has a status lookup, so a row left `Pending` would wait forever for a sync
that never runs for it. `DeliveryStatusRules.InitialFor` sets WhatsApp rows from the provider's
immediate send response: accepted is `Delivered`, refused is `Failed`. Dispatch, resend and
template sends all use it. SMS and Voice still start `Pending` and wait for the sync below. A
data migration resolved existing Pending WhatsApp rows whose job was accepted.

The distinction the design turns on: a recipient the provider says failed is a delivery
failure and belongs on the row, while a request the provider rejected — expired session,
bad credentials — says nothing about any recipient. A rejected request leaves every row
untouched **including `last_checked_at`**, so the next run retries the same window.
Conflating the two writes "undelivered" against messages that arrived.

An unrecognised state is recorded `Unknown`, never guessed into `Failed`, and the raw
payload is stored either way. Chithi ages its reference ids out, so a rejected lookup
(`FAILED` with an empty `smsinfo`) is `Unknown` rather than a delivery failure.

REVE returns one id per batched job, so its lookups are grouped by that id — a
500-recipient batch is one call, not 500 identical ones.

**Voice has no correlation id.** The combined report gives a destination, a start time
and a call state. Records are paired with a number's recipient rows by time
(`Providers:Voice:StatusMatchToleranceMinutes`, default 30; it was 5). One to one when the counts
agree, in chronological order; otherwise left alone and logged rather than guessed.

**Voice never matched a real call until hotfix batch 6.** Proven against the live report: it writes
times as `2026-09-25 08:45:51.629+06`, with an offset. The parser dropped the offset and re-read
the clock in the server's zone, which put every call six hours out; and the provider's call times
sit 6 to 9 minutes away from our dispatch times (measured on real calls from 2026-07-04 and
2026-09-25), outside the old 5 minute tolerance. Times with an offset are now read as stated; a
time with none is business-zone wall clock.

**One sync, no dates (hotfix batch 7, replacing the batch 6 windows).** Sync Now and the nightly job
(`delivery-status-sync`, 00:00 Dhaka) ask about **every recipient still waiting for a status**,
whenever it was sent; the manual dialog is a single button with no date inputs. A date-bounded sync
is what left older rows In Progress: proven on live data, a sync of today returned `updated: 0` while
29 waiting rows (up to 60 days old) sat outside it, and Sync Now resolved all 29. The API still
accepts `from`/`to` (snapped to whole Dhaka days) but nothing in the UI uses it. A run takes at most
`StatusSync:ManualMaxRows` (2000) per channel and the result's `moreWaiting` says when to run again;
the nightly job takes 500 per channel. Voice fetches its report in windows built around the waiting
calls themselves, split where two are over a day apart, so old calls are not lost to a page cap.
Before batch 6 only the previous day was ever queried, so a message a provider first reported as
`SENT` was never revisited (one REVE row stayed In Progress for 19 days). **The result counts what
actually resolved:** `updated` is rows that became Delivered or Failed, `stillWaiting` is rows the
provider still calls in flight, `markedUnknown` is rows given up on this run; it used to count every
row the provider touched, so a sync that changed nothing said "Resolved 27". A row with no clear answer
after 6 hours (`StatusSync:GiveUpAfterHours`, default 6, was 24) becomes `Unknown` instead of staying In
Progress, and Unknown rows sent in the last 72 hours are asked about again so a late delivery report
is not lost (verified live: a fresh SMS stayed `SENT` for three syncs and became `DELIVRD` on the
fourth). REVE
can hold `SENT` indefinitely when the carrier returns no delivery report, and a Voice call the report
never lists cannot be resolved. It is not called Delivered, because nothing said so.

**Every run was throwing before it read a row, found and fixed during the hotfix batch 3
Seq audit.** `StatusSyncService.ResolveWindow` built the default window with
`AppTime.ToAppTime` — a display conversion that returns the business-zone offset
(+06:00) — and handed that straight to a query against `dispatch_job_recipients`' UTC
`timestamptz` columns; Npgsql refuses to write a non-zero-offset `DateTimeOffset` there
at all. The instant was always correct, only the offset needed normalizing to UTC before
use. Fixed by calling `.ToUniversalTime()` on both the default and the explicit-window
path — see the method's own doc comment.

### Resend (R3.2)

Operates on recipient rows, not jobs: SMS and Voice batch many numbers into one job, so
retrying the job would send again to everyone it already reached. Crossing channel types
is refused, the same rule fallback uses. Authorized at the ManualSend tier, not
SuperAdmin: this is operational recovery, not configuration.

**Updates the recipient row in place (hotfix batch 4) — a deliberate reversal of the
original R3.2 design**, recorded here on purpose so it reads as a conscious change, not
drift. Every attempt used to create a new job *and* a new `dispatch_job_recipients` row
linked by `resent_from_recipient_id`, on the reasoning that a retry succeeding must not
erase evidence that the first attempt failed. In practice this meant a recipient retried
three times showed as three separate rows in Message Logs, reading as three different
sends rather than one recipient retried three times — confusing enough that the client
asked for it to stop. Resend now still creates a new `DispatchJob` per attempt (that job
is the attempt's own audit record), but updates the *same* recipient row in place —
`dispatch_job_id`, `provider_reference_id` and `delivery_status` all move to reflect the
latest attempt, so a resend that succeeds stops looking like a failure. Each attempt's own
outcome is preserved separately in `resend_attempts`
(`GET /api/dispatch-jobs/recipients/{id}/attempts`) rather than lost, so "what happened on
attempt 2 vs 3" is still recoverable — just not in the main table. `resent_from_recipient_id`
stays on the entity for reading rows created under the old design; nothing sets it going
forward.

### Balances

`IChannelBalanceProvider` is separate from `IChannelSender`: sending and balance are
independent capabilities. REVE and Chithi report one; WaTrend, NetZero Inbox and
Voice do not, and their channels are omitted from the balance list rather than shown
blank. REVE also reports a credit limit; the others have no such concept, and null there
means *not applicable*, never zero.

Figures are cached on `channel_configs` and refreshed by the `channel-balance-refresh`
Hangfire job every 15 minutes, matching the dashboard's existing cycle. Reads never call
a provider — the dashboard shows several channels at once and one slow vendor would
stall the page. A failed refresh keeps the last known figure and its timestamp and sets
only `balance_error`, so the card shows a stale number with a reason instead of blanking.

**`GET /balance?callerID=…`** (R3.3, API root, unauthenticated; param renamed from
`LABEL` in R3.4) is a third-party integrator's entry point to this same balance data —
a `labelSlug` resolves to an
active, balance-capable channel the same way a gateway tag does — but unlike every
other reader here it calls the provider **live** on every request (a batch-2 hotfix
reversal of the endpoint's original cached-only design; no authentication guards it, so
there is no rate limit on how often that live call fires). A failed live read falls back
to the last cached figure, same graceful-degradation rule as everywhere else. See
[api.md](api.md) for the shape.

`GatewayCredential.Username` ("Username (Balance Client ID)" in the admin UI, R3.3)
auto-syncs to the default SMS channel's own `balanceClientId` whenever that value or the
default SMS channel itself changes (`ChannelConfigService` calls
`GatewayCredentialService.SyncUsernameFromDefaultSmsChannelAsync`) — still admin-editable
between syncs, and never blanked when there is nothing to sync from.

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

When several active policies cover the same failure, the one scoped to the failed
channel wins over a catch-all for its type (hotfix batch 8; before that the winner was
whichever row the database returned first). Only that one policy is tried: if it is
skipped for one of the reasons above, the failure is not passed on to another policy.
A failure no active policy covers is logged at Information ("No active policy covers a
… failure … no retry"), so a lost message can be told apart from a skipped rule.
A fallback that fails too ends the chain: fallback jobs never trigger another fallback.
Fallback runs from the send pipeline only. A recipient resend goes to the channel the
operator picked and has no fallback of its own.

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
- **Configuration precedence**: `Program.cs` loads `authorization.json`, then re-adds the
  environment-variable and command-line sources. The re-add is deliberate — `CreateBuilder`
  registers those before the file, and the last source added wins, so without it the file
  silently outranks every `Providers__*` and `Authorization__*` override set in a
  container. Fixed in R3.0 after an override was set, verified with `printenv` and still
  ignored at dispatch.
- **Middleware order**: GlobalException → ApiLogging (masks `apikey`/`secretkey` in
  stored POST bodies and, since R3.3, GET query strings) → routing/CORS/auth.
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
  instantly. Channel-config reads show secrets **partially masked** (R3.4 — `***` plus the
  last 6 characters, e.g. `***7ced465`; `GET /{id}/reveal?field=` returns one field's real
  value on demand, same SuperAdmin gate); the gateway credentials are read back fully
  decrypted for SuperAdmin, since the pair has to be handed to an integrator and nothing
  else can read the column. Writes treat an omitted or `***`-prefixed field as "leave it
  alone" so a partial save never blanks a credential — one check covers both the old
  full mask and the new partial one.
- **Logging**: Serilog → Seq (compose service, port 18082).
- **Time**: UTC storage/processing everywhere; Dhaka (UTC+6) for display and job
  schedules — see [conventions.md](conventions.md).
- **Background jobs**: Hangfire (same Postgres DB, `hangfire` schema; no dashboard).
  Recurring: `media-cleanup` — Fridays 00:00 server time, deletes expired Media Library
  files and marks them Deleted. Register jobs in Program.cs via `IRecurringJobManager`.
- **Media Library**: authenticated upload (`/api/media`) → local disk
  (`Media:StoragePath`, Docker volume `mediadata`) → public unauthenticated serving at
  `/media/{slug}` for use in outbound messages.
