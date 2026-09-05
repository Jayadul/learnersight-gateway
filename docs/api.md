# API Reference

Load this when: changing endpoints, integrating a client, or debugging request/response shapes.

## Public gateway endpoint

`POST /sendtext` — `application/x-www-form-urlencoded`, lives at the API **root** (not `/api/`).

| Field | Required | Notes |
|---|---|---|
| `toUser` | yes | Comma-separated numbers, digits only with country code (`8801700000001`) |
| `messageContent` | yes | Channel tags (see below); no tag = plain SMS on the default SMS channel |
| `apikey` | yes | Validated against the **gateway credentials** (`gateway_credentials`), not any channel |
| `secretkey` | yes | Same source; constant-time compared |
| `callerID` | yes | Recorded with the request; not part of authentication |

Authentication reads the single `gateway_credentials` row. Its `username` is admin-side
only — callers do not send it, and renaming it cannot break an integration. View and
rotate the keys in the admin UI at Configuration → Gateway Credentials, which reads them
back decrypted for SuperAdmin; the change applies to the next request. See [adr/0007](adr/0007-decouple-gateway-auth-from-channel-config.md) for
why this is no longer the SMS channel's keys.

Responses (REVE-compatible):
- Accepted: `{"Status":"0","Text":"ACCEPTD","Message_ID":"<guid>"}`
- Rejected: `{"Status":"101","Text":"REJECTD"}` (bad/missing credentials or server error)
- Rejected with a fixable reason: `{"Status":"101","Text":"REJECTD","Reason":"Unknown
  channel tag 'sms'. The channel label is now required: [sms:yourlabel::your message]. …"}`
  — `Reason` is omitted when there is nothing for the caller to act on, so the REVE shape
  is unchanged for every other case.

**Fallback does not apply to a multi-tagged send.** If `messageContent` produces more
than one delivery — two tags, or any combined `[sms:a-wp:b::…]` form — the request is
marked multi-channel and no fallback message is generated for any of its jobs, so a send
never costs more than the sender asked for. The skip is logged at Warning and shown on
the message detail page.

### Channel tags

Every tag names both a channel type and the specific gateway that carries it:
`[type:label::content]`. `label` is a channel config's **`labelSlug`** — lowercase
alphanumeric, unique within its channel type.

| Form | Channel |
|---|---|
| `[sms:revemain::text]` | SMS through the channel slugged `revemain` |
| `[wp:waprimary::text]` | WhatsApp text |
| `[wp:waprimary::text\|\|url]` | WhatsApp media — same tag; the URL is what selects media |
| `[sms:a-wp:b::text]` | SMS **and** WhatsApp, one named channel per leg (media goes on the WP leg) |
| `[call:voicemain::campaignId\|\|lotNumber]` | Voice — the campaign to dial, then the lot within it. Omitting `\|\|lotNumber` still parses and falls back to the channel's configured lot, but the integration guide documents only the explicit form |
| *(no tag)* | Plain SMS with the whole body, on the **default** SMS channel |

Rules:

- **The label is mandatory inside a tag.** There is no bare `[sms::…]` shorthand: the only
  route to a default channel is sending no tag at all. Several channels of a type can be
  active at once, so an unlabelled tag would silently pick whichever happened to be
  default.
- Channel type is read from the tag keyword, so `label_slug` only needs to be unique
  **within** a channel type — there is no global uniqueness constraint.
- Combined tags only ever pair `sms:` and `wp:`; `call:` never combines. Parsing splits on
  the literal `-wp:`, which is unambiguous because `sms`/`wp`/`call` are reserved and a
  slug cannot contain a hyphen or a colon.
- Only **active** channels are addressable. An unknown label, or one naming an inactive
  channel, **rejects the whole request** rather than falling back to the default — a typo
  must never bill a channel the sender did not name.
- Tags are case-insensitive: `[SMS:REVEMAIN::hi]` resolves the same as `[sms:revemain::hi]`.
- An unrecognised tag **rejects the whole request** with a `Reason` naming it and its
  replacement. It is never dropped: a skipped tag produces no job and no error, so the
  caller would read a success for a message that was never sent.

> **Release 2.1 removed the previous grammar outright.** `[sms::…]`, `[wp::…]`,
> `[call::…]`, `[sms-wp::…]`, `[smswp::…]`, `[wpmedia::…]` and every `_label:` form now
> reject. Rejection reasons name the replacement, because a caller hitting one is
> mid-migration. Existing integrators must update both their templates **and** their
> credentials — see adr/0007.

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
  FallbackPolicy, GatewayCredential.

### Controller map

| Controller | Route | Purpose |
|---|---|---|
| Auth | `/api/auth` | login / refresh |
| Dashboard | `/api/dashboard` | summary, timeline, breakdown, failure reasons. Each failure reason carries `byChannel` — the same failures split by channel type and gateway, so a one-provider outage is distinguishable from a general problem |
| MessageRequest | `/api/message-requests` | paged logs + detail with job tree. `isMultiChannel` marks a request whose tags asked for more than one delivery — fallback is skipped for it. `?channelConfigId=` filters on the **gateway** that delivered (matching the jobs, since a request records only channel types) |
| DispatchLog | `/api/dispatch-logs` | paged delivery jobs. Each job carries `channelLabel` — the name of the channel that actually sent it, since several gateways of a type may be active — and `campaignId` on voice jobs |
| ApiLog | `/api/api-logs` | gateway request audit (secrets masked). `requestedChannels` snapshots the channel types the tags asked for ("SMS, WhatsApp") and `?channel=` filters on it; `messageRequestId` links to the deliveries the request produced |
| ManualSend | `/api/manual-send` | admin-initiated sends. Credentials come from the channel config; every override is optional and a blank one keeps the configured value — `channelLabelSlug` (which gateway; default when omitted), `callerId` (SMS), `campaignId` and `lotNumber` (Voice), `mediaUrl` (WhatsApp — its presence is what makes the send a media send) |
| StatusSync | `/api/status-sync` | `GET /channels` lists channels whose provider offers a delivery status lookup (REVE, Chithi, NetZero Voice — not GenNet, which has none). `POST /channels/{id}` runs one now; omitting the window uses the previous full day, as the nightly job does. A failed run reports `success:false` and changes nothing, so the window can be retried |
| Resend | `/api/dispatch-jobs/recipients/resend` | Retries specific `dispatch_job_recipients` rows, optionally through another **active channel of the same type** — crossing types is refused. Creates new jobs and rows rather than mutating the originals. Capped at 200 recipients per request; duplicates in the selection are sent once. ManualSend tier, not SuperAdmin |
| DispatchLog | `/api/dispatch-logs` | Adds a `deliveryStatus` filter (R3.2) matching jobs with **any** recipient in that state — a Sent job can hold a mix, so it sits alongside the job-level `status` rather than replacing it |
| ChannelBalance | `/api/channel-balances` | `GET` returns cached balances for every channel whose provider reports one (REVE, GenNet, Chithi) — it never calls a provider. `POST /{id}/refresh` reads one channel's balance now. A failed read keeps the last known figure and sets `error`, so `isStale` is true and the caller shows the old number with a marker rather than a blank. `creditLimit` is null where the provider has no such concept |
| ChannelConfig | `/api/channel-configs` | CRUD + `PATCH /{id}/activate`, `/deactivate`, `/set-default`. `provider` (R3.0) names the vendor and is validated against the channel type on save — a provider that cannot serve the type is rejected there rather than at dispatch; `providerName` is display text. A `baseUrl` config field points that channel at its own host, blank meaning the configured default. Several configs per type may be active; exactly one is the **default**. The default cannot be deactivated or deleted — promote another first; a channel named by a fallback rule cannot be deleted until that rule is changed. Reads mask secret fields as `***` — on `PUT`, a config field sent as `***` or omitted keeps its stored value, so saving back a masked read never overwrites credentials |
| FallbackPolicy | `/api/fallback-policies` | CRUD + `PATCH /{id}/toggle-active`. Each side carries a channel **type** and an optional `triggerChannelConfigId` / `fallbackChannelConfigId` naming one channel (null = any/default of that type). Same-type rules must name **both** channels and they must differ; differing types are rejected unless `allowCrossChannel` is true. Reads also return `triggerChannelLabel` / `fallbackChannelLabel` |
| AdminUser | `/api/admin-users` | user CRUD |
| GatewayCredential | `/api/gateway-credentials` | `GET` + `PUT` on the collection path — one row, so no id in the route. **Reads return both keys decrypted** (SuperAdmin-only; the pair has to be readable to be handed to an integrator, and the column is ciphertext). `isReadable` is false when the stored ciphertext will not decrypt. On `PUT` every field is optional: an omitted, blank, or `***` field keeps its stored value, so changing the username alone cannot blank the keys |
| Health | `/health` | Anonymous readiness probe used by the container healthcheck. Returns `{"status":"healthy"}` once migrations and seeding have finished — the app does not listen before then |
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
