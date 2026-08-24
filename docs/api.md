# API Reference

Load this when: changing endpoints, integrating a client, or debugging request/response shapes.

## Public gateway endpoint

`POST /sendtext` — `application/x-www-form-urlencoded`, lives at the API **root** (not `/api/`).

| Field | Required | Notes |
|---|---|---|
| `toUser` | yes | Comma-separated numbers, digits only with country code (`8801700000001`) |
| `messageContent` | yes | Channel tags (see [architecture.md](architecture.md)); no tag = plain SMS |
| `apikey` | yes | Validated against active SMS ChannelConfig |
| `secretkey` | yes | Same source; constant-time compared |
| `callerID` | yes | Recorded with the request |

Responses (REVE-compatible):
- Accepted: `{"Status":"0","Text":"ACCEPTD","Message_ID":"<guid>"}`
- Rejected: `{"Status":"101","Text":"REJECTD"}` (bad/missing credentials or server error)
- Rejected with a fixable reason: `{"Status":"101","Text":"REJECTD","Reason":"Unknown
  channel tag 'smswp'. It is now [sms-wp::text]."}` — `Reason` is omitted when there is
  nothing for the caller to act on, so the REVE shape is unchanged for every other case.

**Fallback does not apply to a multi-tagged send.** If `messageContent` produces more
than one delivery — two tags, or any `[sms-wp::..]` form — the request is marked multi-channel and no fallback message is generated for any
of its jobs, so a send never costs more than the sender asked for. The skip is logged at
Warning and shown on the message detail page.

### Channel selection

Each channel config has a **`labelSlug`** (lowercase alphanumeric, unique per channel
type). A tag may name one explicitly:

| Form | Resolves to |
|---|---|
| `[sms::text]` | the **default** SMS channel |
| `[sms_label:revemain::text]` | the **active** SMS channel whose slug is `revemain` |
| `[wp_label:slug::text]` / `[call_label:slug::id]` | same, for WhatsApp and Voice |
| `[sms_label:a-wp_label:b::text]` | one named channel per leg |
| `[sms_label:a-wp::text]` / `[sms-wp_label:b::text]` | one leg named, the other on its type's default |

Only **active** channels are addressable by slug, and an unknown or inactive slug is
**rejected rather than falling back to the default** — a typo must never bill a channel
the sender did not name. Slugs exclude hyphens because the combined form splits on `-wp`,
which is also why either leg can be labelled independently.

An unrecognised tag — including the retired `smswp` and `wpmedia` — **rejects the whole
request** with a `Reason` naming it and its replacement. It is never dropped: a skipped
tag produces no job and no error, so the caller would read a success for a message that
was never sent.

Gateway `apikey`/`secretkey` are validated against the **default SMS channel** only;
credentials on non-default channels do not authenticate callers.

Live simulator + shareable integration guide: admin UI → Administration → Gateway
Simulator (SuperAdmin only). Postman collection: `postman/`.

## Admin API (`/api/*`, JWT Bearer)

- Wire format: **enums as integers** (ChannelType SMS=0, WP=1, Voice=3 — **2 is retired**,
  see below; MessageStatus Pending=0, PartialSuccess=1, Success=2, Failed=3, **Fallback=4**
  — every primary delivery failed but a retry got through, so the message arrived by a
  route the sender did not ask for; JobStatus
  Pending=0, Sent=1, Failed=2; AdminRole SuperAdmin=0, Admin=1; MediaStatus Active=0,
  Deleted=1).
- **ChannelType 2 (`WP_Media`) no longer exists.** WhatsApp text and media were unified
  into `WP=1`; a message is media when it carries a `mediaUrl`, not by channel type.
  Sending `channelType=2` now returns 400. `Voice` deliberately stays 3, so Voice
  integrations are unaffected — the gap at 2 is intentional and must not be reused.
- Errors: ASP.NET ProblemDetails — clients must read `detail`/`title` (the UI error
  interceptor does).
- Auth: `POST /api/auth/login` → access token (15 min) + refresh token (7 days);
  `POST /api/auth/refresh`. **Do not change token lifetimes** — explicit client decision.
- Authorization per controller comes from `authorization.json`
  (see `NetZeroGateway.API/CLAUDE.md`). SuperAdmin-only: AdminUser, ChannelConfig,
  FallbackPolicy.

### Controller map

| Controller | Route | Purpose |
|---|---|---|
| Auth | `/api/auth` | login / refresh |
| Dashboard | `/api/dashboard` | summary, timeline, breakdown, failure reasons. Each failure reason carries `byChannel` — the same failures split by channel type and gateway, so a one-provider outage is distinguishable from a general problem |
| MessageRequest | `/api/message-requests` | paged logs + detail with job tree. `isMultiChannel` marks a request whose tags asked for more than one delivery — fallback is skipped for it. `?channelConfigId=` filters on the **gateway** that delivered (matching the jobs, since a request records only channel types) |
| DispatchLog | `/api/dispatch-logs` | paged delivery jobs. Each job carries `channelLabel` — the name of the channel that actually sent it, since several gateways of a type may be active — and `campaignId` on voice jobs |
| ApiLog | `/api/api-logs` | gateway request audit (secrets masked). `requestedChannels` snapshots the channel types the tags asked for ("SMS, WhatsApp") and `?channel=` filters on it; `messageRequestId` links to the deliveries the request produced |
| ManualSend | `/api/manual-send` | admin-initiated sends. Credentials come from the channel config; every override is optional and a blank one keeps the configured value — `channelLabelSlug` (which gateway; default when omitted), `callerId` (SMS), `campaignId` and `lotNumber` (Voice), `mediaUrl` (WhatsApp — its presence is what makes the send a media send) |
| ChannelConfig | `/api/channel-configs` | CRUD + `PATCH /{id}/activate`, `/deactivate`, `/set-default`. Several configs per type may be active; exactly one is the **default**. The default cannot be deactivated or deleted — promote another first; a channel named by a fallback rule cannot be deleted until that rule is changed. Reads mask secret fields as `***` — on `PUT`, a config field sent as `***` or omitted keeps its stored value, so saving back a masked read never overwrites credentials |
| FallbackPolicy | `/api/fallback-policies` | CRUD + `PATCH /{id}/toggle-active`. Each side carries a channel **type** and an optional `triggerChannelConfigId` / `fallbackChannelConfigId` naming one channel (null = any/default of that type). Same-type rules must name **both** channels and they must differ; differing types are rejected unless `allowCrossChannel` is true. Reads also return `triggerChannelLabel` / `fallbackChannelLabel` |
| AdminUser | `/api/admin-users` | user CRUD |
| Backup | `/api/backup/export?from=&to=` | xlsx download for a date range |
| Media | `/api/media` | Media Library: multipart upload (`file`, `customName?`, `expiryDays=90`), paged list `?search=&page=&pageSize=` (Active rows only — soft-deleted rows are audit history), detail, `DELETE /{id}` (removes file, keeps row as Deleted) |
| PublicMedia | `/media/{fileName}` | **no auth** — serves uploaded files by slug; `?download=true` forces attachment |

## Media Library notes

- Stored names are URL slugs (`Company Logo.png` → `company-logo.png`); duplicates get a
  numeric suffix (`company-logo-2.png`) — the response's `storedFileName`/`publicUrl` are
  authoritative.
- `publicUrl` is built from `Media:PublicBaseUrl` config at upload time.
- The `/media/*` URL namespace at the web origin is reserved for these public files
  (nginx proxies it to the API) — the admin page lives at `/media-library` for this
  reason; never add an SPA route under `/media`.
- Validation: max size `Media:MaxFileSizeMb` (default 25), extension allowlist
  `Media:AllowedExtensions`, expiry 1–3650 days.
- Expired or deleted files return 404 from `/media/{fileName}`; a Hangfire job
  (`media-cleanup`, Fridays 00:00 server time) deletes expired files from disk and marks
  rows Deleted. Manual `DELETE /api/media/{id}` uses the same logic and records
  `deletedAt`/`deletedBy` (`system` when the job did it); rows are never removed.

## Swagger

Development only (`http://localhost:5212/swagger`), two groups: `gateway` and `admin`.
**Never enable Swagger in production** — standing client instruction.
