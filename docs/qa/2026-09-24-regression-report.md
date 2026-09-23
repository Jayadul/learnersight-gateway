# Deep QA regression report, 2026-09-24

Scope: `/sendtext`, every page touched by hotfix batches 3 to 5, and the media upload path.
Branch `qa/deep-qa-media-path` in both repos (nothing pushed). Backend: 174 unit tests pass;
frontend: `ng build` passes.

## How this was run

Every result below comes from a real request against the running Docker stack, not from
reading code. Two limits shaped the method, both agreed beforehand:

- **Providers were stubbed for scale and dispatch tests.** A local stub server answers in each
  provider's response shape (REVE, Chithi, WaTrend, NetZero Inbox v1, Inbox QR, NetZero Voice).
  Test channels pointed their own `baseUrl` at it, so 950-recipient runs cost nothing and no
  call or message reached a real recipient. **NetZero Voice used fake credentials and the
  stub, so no real call was possible.** Real-provider behaviour rests on the earlier live tests
  (batches 3 to 5) plus the small number of real calls listed under "Side effects".
- **No browser automation** (project rule). Everything behind the UI was verified through the
  API, the database and the built bundle. Cases that only a person clicking can prove are
  marked **Manual** and collected in the checklist at the end.

Status key: **Pass**, **Fixed** (was broken, fixed, re-tested), **Manual** (needs your hands),
**Observation** (works as built; a decision or note for you).

## Summary

| Result | Count |
|---|---|
| Pass | 63 |
| Fixed | 7 defects (6 re-tested live, 1 needs your eyes: the date-range fix, item 2.8) |
| Manual | 10 test cases, listed at the end |
| Observation | 7 (O1 to O7) |

Defects found and fixed (each has its own commit):

| # | Defect | Commit |
|---|---|---|
| 1 | Any recipient list over about 140 numbers was rejected with a bare `101 REJECTD` (columns were `varchar(2000)`); a large `GET` also answered `414` | backend `9ae1416`, frontend `d14d1bf` |
| 2 | The seeder replaced a seeded channel's whole config on every boot: admin edits reverted and added fields vanished at each restart | backend `7f43d3e` |
| 3 | Date-range filters excluded the whole end day; a single-day range returned nothing (Message Logs, API Logs) | frontend `52632d3` |
| 4 | Dashboard "today" and the timeline used the UTC day, so counters reset at 06:00 Dhaka time | backend `2743672` |
| 5 | Manual Send rejected a Voice send that left Campaign empty, contradicting the form's own hint | backend `64fabcd` |
| 6 | `HEAD` on a public media link returned 405 (providers may probe with HEAD) | backend `559c3f6` |
| 7 | Media links were frozen at upload time, so files uploaded under the shipped `localhost` base URL kept unreachable links after it was corrected | backend `559c3f6` |

## Task 1: `/sendtext`

| ID | Tested | Expected | Actual | Status |
|---|---|---|---|---|
| 1.1 | POST `application/json` | `0 ACCEPTD`, provider receives it | Accepted; provider received it. Also proven live against the real default channel in batch 5 | Pass |
| 1.2 | POST `application/x-www-form-urlencoded` | Same | Same | Pass |
| 1.3 | GET with query string | Same | Same | Pass |
| 1.4 | `[sms:label::text]` | One SMS job | Sent | Pass |
| 1.5 | `[wp:label::text]` (WaTrend) | One WhatsApp text job | Provider received `type=text` | Pass |
| 1.6 | `[wp:label::text\|\|url]` | Media send | Provider received `type=media` with `media_url` | Pass |
| 1.7 | `[sms:a-wp:b::text]` | Two jobs, one per leg | Two jobs, both sent | Pass |
| 1.8 | Combined tag with a media URL | Media on the WhatsApp leg | WhatsApp leg received the URL as media. The SMS leg's text keeps the raw `text\|\|url` | Pass (see Observation O3) |
| 1.9 | `[call:label::campaign\|\|lot]` | Voice job with campaign and lot | Stub received `isArray=true`, campaign and lot as sent | Pass |
| 1.10 | No tag | Plain SMS on the default SMS channel | Sent (real default channel, one send) | Pass |
| 1.11 | REVE, Chithi, WaTrend | Dispatch | All dispatched | Pass |
| 1.12 | Inbox v1 text | Dispatch | Dispatched | Pass |
| 1.13 | Inbox v1 media attempt | Clean rejection, not mangled | Job `Failed`: "This WhatsApp channel does not support media..." and nothing reached the provider | Pass |
| 1.14 | Inbox QR text and image | Dispatch | Text and `imageUrl` both reached the provider | Pass |
| 1.15 | NetZero Voice | Dispatch | Dispatched to the stub with fake credentials; no real call | Pass |
| 1.16 | Wrong `apikey`, wrong `secretkey`, missing keys (form and JSON) | `101 REJECTD` | `101 REJECTD` each, no reason (deliberate: it does not reveal which key was wrong) | Pass |
| 1.17 | Unknown label, inactive label, old tag grammar | `101 REJECTD` with a clear reason | Reason names the label or gives the new grammar | Pass |
| 1.18 | Malformed JSON, `text/plain`, empty `toUser` | Not a 500 | `400` problem details, `415`, `400` | Pass (see O4) |
| 1.19 | **950 recipients, GET, POST form, POST JSON** | Accepted and every number reaches the provider | **Rejected `101 REJECTD`; GET `414`.** Cause: `message_requests.to_user`, `dispatch_jobs.to_number`, `api_logs.to_user` were `varchar(2000)`; Kestrel and nginx capped the request line at 8k | **Fixed** |
| 1.20 | 950 recipients after the fix, REVE (GET direct, GET via nginx, POST form, POST JSON) | 950 of 950 at provider | 950 of 950 each; database shows `4 jobs × 950 = 3800` recipient rows | Pass |
| 1.21 | 950 recipients, Chithi, WaTrend, Inbox v1, Inbox QR (GET via nginx) | 950 of 950 | 950 of 950 each (about 24 s for the per-recipient providers at zero delay) | Pass |
| 1.22 | 950 recipients, Voice (POST form 3, POST JSON 950, GET 950) | One JSON array of all numbers | `isArray=true`, `n=950`, `n=3` | Pass |
| 1.23 | Single-tag send, primary fails | Fallback to the policy's channel | After the rule was active: retry on Chithi, badged `Fallback`, recipient status `Fallback` | Pass |
| 1.24 | Combined tag and two-tag send, one leg fails | No fallback | No retry job created; request shows Success because the other leg delivered | Pass |
| 1.25 | Special characters through GET, POST form, POST JSON: `- : \|\| & = + % #`, quotes, Bangla, `✓`, `é` | Byte-identical at the provider | Identical on all three routes | Pass |
| 1.26 | `GET /balance?callerID=` valid SMS labels | Balance JSON | `smsdefault` and `chithisms` returned balances | Pass |
| 1.27 | `/balance` unknown, Voice, inactive label; no parameter | Clear `404` | `{"status":"error","message":"Unknown or unsupported label"}` | Pass |
| 1.28 | `/balance` needs no auth and is GET-only | No token; POST refused | Worked without a token; POST returns `405` | Pass |

## Task 2: page sweep

Endpoint and data checks were run for every page; visual and interaction cases are under **Manual**.

### Dashboard

| ID | Tested | Result | Status |
|---|---|---|---|
| 2.1 | Summary, timeline, channel breakdown, failure reasons (with `byChannel`), channel balances: shapes and role access | All correct; Operator gets 200 | Pass |
| 2.2 | "Today" boundary | Used the UTC day; now the Dhaka day via `AppTime.StartOfDayUtc` (3 tests). Live after the fix: `totalToday` 3 and the timeline matches the database grouped by Dhaka date (the UTC day would have shown 0) | **Fixed** |
| 2.3 | Auto-refresh every 15 minutes | Source sets a 15-minute countdown and reload | Manual (confirm on screen) |
| 2.4 | Long failure reasons truncate with tooltip; click opens the underlying records | Source has ellipsis, tooltip and click handlers; the drill-down query (`failureReason=`) returns the right rows | Manual for the visual |
| 2.5 | Balance card per channel, provider shape | Balances returned per channel with `isStale` and `error` | Pass |

### Message Logs

| ID | Tested | Result | Status |
|---|---|---|---|
| 2.6 | One row per recipient | 950-recipient jobs produce 950 rows | Pass |
| 2.7 | Filters: channel type, status (including `Fallback`), channel, failure reason, recipient, date range | Each returns the database's count; `Fallback` returned the one fallback row | Pass |
| 2.8 | Date range as the UI sends it | End day was excluded (midnight sent as the range end; the API returned 0 rows for a single day and the right count for full-day ISO bounds). The filter bar now sends the end of the day. Build passes; the click itself needs step 2 of the manual checklist | **Fixed** (confirm by hand) |
| 2.9 | Pagination | Page 1 and page 2 share no ids | Pass |
| 2.10 | Bulk same-channel resend, provider still failing, twice | Three rows stay three rows; six attempts recorded | Pass |
| 2.11 | Resend that then succeeds | Same row updates to `Pending` with the new reference; history lists all three attempts | Pass |
| 2.12 | Cross-channel resend within a type | Row moves to the other SMS channel, still one row | Pass |
| 2.13 | Cross-type resend, 201 ids, empty selection | Refused with a clear `400` each | Pass |
| 2.14 | Historic (pre-per-recipient) data | 33 backfilled rows return and render | Pass |
| 2.15 | Detail data: message, channel, date, exact raw provider response | Present on every row (Chithi and REVE checked) | Pass |
| 2.16 | Status wording, truncation tooltip, serial numbers, modal layout | UI only | Manual |

### API Logs

| ID | Tested | Result | Status |
|---|---|---|---|
| 2.17 | Secret masking for form, JSON and GET requests | `apikey` and `secretkey` are `***` in all three; zero plaintext copies anywhere in `api_logs` | Pass |
| 2.18 | Channel and status filters | Channel `SMS`/`WP`/`Voice` and status `200`/`400` return counts | Pass |
| 2.19 | Very long values | A 13,299-character recipient list stored and returned | Pass |
| 2.20 | Curl reconstruction, table layout with long values | UI only | Manual |

### Channel Config

| ID | Tested | Result | Status |
|---|---|---|---|
| 2.21 | Create REVE, Chithi, WaTrend, Inbox v1, Inbox QR and Voice channels | All `201` | Pass |
| 2.22 | **Edit one field, per provider, two ways**: masked round-trip (as the form sends it) and partial-only. Compare every field afterward through reveal | 12 of 12: change applied, nothing lost, nothing changed | Pass |
| 2.23 | Restart the API after an admin edit to a seeded channel | **Edit reverted, added field deleted.** The seeder replaced the whole config, contradicting `docs/database.md` | **Fixed**, proven across a restart on the real seeded QR channel; then restored |
| 2.24 | REVE balance fields (`balanceBaseUrl`, `balanceClientId`, `balanceSessionCookie`) | Saved; cookie masked with a working reveal; other fields intact | Pass |
| 2.25 | Partial masking and reveal | `***` plus last 6 characters; reveal returns the real value; SuperAdmin only | Pass |
| 2.26 | Default rules | Deactivating or deleting a default, and deleting a channel named by a fallback rule, all refused with clear text; exactly one default per type | Pass |
| 2.27 | Set-default on an inactive channel | Also activates it (by design: a default must be usable) | Observation O7 |
| 2.28 | "Balance API" and "Delivery Status Sync" sections | The form has no such sections; they are plain provider fields, covered by 2.22 and 2.24 | Pass |

### Fallback Policies, Manual Send, Settings

| ID | Tested | Result | Status |
|---|---|---|---|
| 2.29 | Same-channel both sides, missing ids, cross-type without the flag | Each refused with a clear `400`; cross-type with the flag accepted | Pass |
| 2.30 | Cascading type-to-channel dropdowns, cross-type warning | UI only | Manual |
| 2.31 | Manual Send SMS with `callerId` override | Provider received `callerID=OVERRIDE` | Pass |
| 2.32 | Manual Send Voice with `campaignId` and `lotNumber` | Provider received `camp-override` and lot `42` | Pass |
| 2.33 | Manual Send Voice with both left empty | **`400 Enter a message to send.`** although the form says to leave it empty | **Fixed** (2 new tests) |
| 2.34 | Manual Send WhatsApp media on WaTrend; on Inbox v1 | Media delivered; Inbox v1 fails cleanly with the reason | Pass |
| 2.35 | Manual Send validation: no recipients, commas only, no message | Clear `400` each | Pass |
| 2.36 | Manual Send at 950 recipients, SMS and WhatsApp | 950 of 950 each | Pass |
| 2.37 | WhatsApp Template errors: channel without token, wrong provider, unknown channel, blank name | Clear `400`/`404` each (the token override itself was proven live in batch 5) | Pass |
| 2.38 | Send does not reload the page or reset the tab | UI only (code resets only the form fields) | Manual |
| 2.39 | Gateway credentials username follows the default SMS channel | Changing the default moved `username` from `netzero-gateway` to `net zero` | Pass |
| 2.40 | Gateway Simulator: POST and GET, Balance card, ordering, no overlap, guide text says `application/json` | Served bundle carries the new text (batch 5); layout is UI only | Manual |

### Sync, roles

| ID | Tested | Result | Status |
|---|---|---|---|
| 2.41 | Sync now, REVE, Chithi, Voice, real window 20 to 24 Sep | All `success:true`; REVE updated 11 rows | Pass |
| 2.42 | Failure shapes: REVE `REJECTD` to Failed, Chithi rejected lookup to Unknown, Voice rejected report touches no row | Pinned by existing unit tests (`StatusSyncProviderTests`) | Pass |
| 2.43 | Operator role: dashboard, message logs, resend, API logs, media, manual send channel lists, manual send | 12 of 12 endpoints `200`; manual send `202`, resend `200`. SuperAdmin-only endpoints return `403` | Pass |
| 2.44 | Responsive layout on every page | UI only | Manual |

## Task 3: media path

| ID | Tested | Expected | Actual | Status |
|---|---|---|---|---|
| 3.1 | API container user and folder | `app` can write `/app/media` | `uid=app`, `/app/media` owned `app:app`, write test passed | Pass |
| 3.2 | Storage volume | Named volume, outside build context | `netzero-gateway_mediadata` mounted read-write | Pass |
| 3.3 | Upload through the web proxy, then fetch the link with no login | `200`, correct type | `200 image/png` on both `:18080` and `:18081`; file on disk owned `app:app` | Pass |
| 3.4 | Unknown file, path traversal, disallowed extension | Refused | `404`, `400`, `400` | Pass |
| 3.5 | `HEAD` on a public link | `200` | `405` | **Fixed** (`200`, right content type) |
| 3.6 | Public URL after correcting `Media:PublicBaseUrl` | Existing files follow | Frozen at upload time | **Fixed** (built from the current value; 3 tests) |
| 3.7 | Base URL in the shipped `deploy/docker-compose.yml` | The hosted address | **`http://localhost:18080`**; unreachable by any provider | Not changed here (see below) |
| 3.8 | Warning when the base URL is local | Visible in logs | Seq warning at every upload while it is empty or local | Pass |
| 3.9 | Link reachable from outside the hosted server | `200` from another network | **Not tested: no access to the hosted server** | Manual |
| 3.10 | WhatsApp media delivery using that link | Delivered | **Not tested** (needs the hosted link and a WhatsApp provider) | Manual |

`deploy/docker-compose.yml` is outside git and needs your domain or server address, so I left
it alone. Set `Media__PublicBaseUrl` there before the first upload on the hosted server.
`docs/deployment.md` now carries a four-step hosted checklist.

## Observations for your decision

- **O1. Dashboard counts dispatch jobs, not recipients.** "SMS sent today" showed 23 while about
  5,700 SMS recipients were sent, because REVE and Voice batch a whole request into one job while
  WhatsApp has one job per recipient. The three channels are not comparable, and a 900-recipient
  SMS blast counts as 1. Switching to recipient rows fixes it but changes every KPI the client
  is used to, so I did not change it.
- **O2. No server-side number validation.** `/sendtext` and Manual Send both accept `abc` as a
  number; only the UI's validator enforces digits-only. The provider decides what happens next.
- **O3. Combined tag with media.** The SMS leg keeps the raw `text||url`. This is deliberate and
  pinned by an existing test (the recipient keeps the only copy of the link), but the guide page
  says the URL "goes on the WhatsApp leg", which reads as if the SMS is clean.
- **O4. Malformed JSON gets `400` problem details, not `101 REJECTD`.** Fine for developers, but
  a REVE-compatible integrator may expect the `Status/Text` shape.
- **O5. Manual Send with an unknown label returns `202 queued`** and then fails without any job
  rows, unlike `/sendtext`, which rejects at once with a reason. The UI picks from a list, so
  this needs a hand-made request.
- **O6. Dashboard loads every request and job into memory** on each call
  (`GetAllAsync`). Fine at today's volume; it will slow down as the tables grow.
- **O7. Set-default activates the channel** (deliberate). Noted because the "default cannot be
  inactive" rule is enforced this way rather than by refusing.

## Side effects left in the environment

- Gateway credentials `username` is now `net zero` (was `netzero-gateway`). This is the
  documented auto-sync to the default SMS channel's Balance Client ID, triggered by my
  default-channel test. Key and secret are untouched and `/sendtext` still authenticates.
- Three calls reached the **real** default REVE channel: the no-tag send (item 1.10) and one
  stray Manual Send with the number `abc` (I omitted the label, so it used the default). Sync now
  (2.41) also read real REVE, Chithi and Voice status.
- All test channels, the fallback rule, the test media and about 12,400 test recipient rows were
  removed afterward. Real history is untouched.

## Manual checklist (needs a person at the screen)

1. **Dashboard**: leave it open 15 minutes and confirm it refreshes; hover a long failure reason
   for the tooltip; click a reason and a channel pill and confirm the records shown match.
2. **Message Logs**: pick one day for both dates and confirm rows appear (item 2.8); check the
   status wording, that content truncates with a tooltip, serial numbers count from 1 on each
   page, and the Fallback filter; select two failed rows and resend, once same channel and once
   through another channel of the same type.
3. **API Logs**: open a row and confirm the curl command is complete with secrets masked; check a
   long recipient list does not break the table.
4. **Channel Config**: open each provider's edit dialog, change one field, save, reopen; reveal a
   credential; confirm the Balance Base URL, Client ID and Session Cookie fields on a REVE channel.
5. **Fallback Policies**: pick a type and confirm the channel dropdown fills; choose two different
   types and confirm the cross-channel warning appears.
6. **Manual Send**: send on SMS, WhatsApp and Voice and confirm you stay on the same tab with the
   channel kept; leave Voice Campaign empty and confirm it now sends.
7. **Gateway Simulator**: send by POST and GET; check the Balance card and that no labels overlap;
   confirm the guide says `application/json` and shows the JSON example.
8. **Every page at a phone width** (about 400 px).
9. **Hosted server**: follow the four-step checklist in `docs/deployment.md`, including a real
   WhatsApp media send with an uploaded file's link.
