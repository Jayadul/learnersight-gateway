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

Live simulator + shareable integration guide: admin UI → Administration → Gateway
Simulator (SuperAdmin only). Postman collection: `postman/`.

## Admin API (`/api/*`, JWT Bearer)

- Wire format: **enums as integers** (ChannelType SMS=0, WP_Text=1, WP_Media=2, Voice=3;
  MessageStatus Pending=0, PartialSuccess=1, Success=2, Failed=3; JobStatus Pending=0,
  Sent=1, Failed=2; AdminRole SuperAdmin=0, Admin=1; MediaStatus Active=0, Deleted=1).
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
| Dashboard | `/api/dashboard` | summary, timeline, breakdown, failure reasons |
| MessageRequest | `/api/message-requests` | paged logs + detail with job tree |
| DispatchLog | `/api/dispatch-logs` | paged delivery jobs |
| ApiLog | `/api/api-logs` | gateway request audit (secrets masked) |
| ManualSend | `/api/manual-send` | admin-initiated sends (creds from active configs) |
| ChannelConfig | `/api/channel-configs` | CRUD + `/activate`; active config cannot be deleted |
| FallbackPolicy | `/api/fallback-policies` | CRUD + `PATCH /{id}/toggle-active` |
| AdminUser | `/api/admin-users` | user CRUD |
| Backup | `/api/backup/export?from=&to=` | xlsx download for a date range |
| Media | `/api/media` | Media Library: multipart upload (`file`, `customName?`, `expiryDays=90`), paged list `?search=&page=&pageSize=` (Active rows only — soft-deleted rows are audit history), detail, `DELETE /{id}` (removes file, keeps row as Deleted) |
| PublicMedia | `/media/{fileName}` | **no auth** — serves uploaded files by slug; `?download=true` forces attachment |

## Media Library notes

- Stored names are URL slugs (`Company Logo.png` → `company-logo.png`); duplicates get a
  numeric suffix (`company-logo-2.png`) — the response's `storedFileName`/`publicUrl` are
  authoritative.
- `publicUrl` is built from `Media:PublicBaseUrl` config at upload time.
- Validation: max size `Media:MaxFileSizeMb` (default 25), extension allowlist
  `Media:AllowedExtensions`, expiry 1–3650 days.
- Expired or deleted files return 404 from `/media/{fileName}`; a Hangfire job
  (`media-cleanup`, Fridays 00:00 server time) deletes expired files from disk and marks
  rows Deleted. Manual `DELETE /api/media/{id}` uses the same logic and records
  `deletedAt`/`deletedBy` (`system` when the job did it); rows are never removed.

## Swagger

Development only (`http://localhost:5212/swagger`), two groups: `gateway` and `admin`.
**Never enable Swagger in production** — standing client instruction.
