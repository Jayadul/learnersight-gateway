# Deployment & Operations

Load this when: building/pushing images, running the stack, debugging containers, or backups.

## Stack (compose at workspace root, one level above this repo)

| Service | Image | Port | Notes |
|---|---|---|---|
| db | postgres:16-alpine | 15432 | volume `pgdata` |
| seq | datalust/seq | 18082 | log viewer, volume `seqdata` |
| api | built from this repo | 18081 | migrations+seed on startup, then `HEALTHCHECK` on `/health`; volume `mediadata:/app/media` for Media Library files |
| web | built from `../learnersight-netzero-gateway-ui` | 18080 | nginx serves SPA, proxies `/api/`, `/sendtext` and `/media/`. Waits for `api: condition: service_healthy` |
| backup | built from `../backup` | — | nightly exports, writes next to compose file |

**Provider endpoints (R3.1).** GenNet (`isms.gennet.com.bd`), Chithi (`sms.chithi.net`)
and both NetZero Inbox APIs (`inbox.netzero.com.bd` — `https` for v1, `http` for the QR
device endpoint, which is why they are separate entries) have defaults under `Providers:*` in
`authorization.json`, but unlike REVE/WaTrend/Voice their channels are seeded with an
explicit `baseUrl`, since a blank one on a provider without a central default leaves the
channel nowhere to send. REVE's *balance* uses a separate host (`https://smpp.revesms.com`,
no port) from its send endpoint and is configured per channel as `balanceBaseUrl`.

These provider secrets are still seeded in `appsettings.json` in plaintext — R1 debt
item 4, now covering six channels rather than three. Rotate the REVE session cookie, the
shared GenNet/Chithi `api_token` and the NetZero Inbox token before they carry
production traffic. **Hotfix batch 5** added the NetZero Inbox WhatsApp Template API
Token to the same seed block so a fresh database starts with it; **batch 6** moved it to its own
seeded channel, "WA Template" (`NetZeroInboxTemplate`), instead of a field on the v1 channel. A
`templateToken` already stored on the v1 channel stays there unused (the admin API cannot remove a field). (Before the deep QA
pass a value set only through the admin API was also wiped on the next restart; the
seeder now leaves fields it does not carry alone, see database.md. A seeded credential
still refreshes from the config on every boot.) Same rotation caveat applies before this
carries production traffic.

**Environment overrides work as of R3.0.** `Providers__Voice__BaseUrl` and friends were
silently ignored before it: `authorization.json` loaded after the environment source and
won. If a deployment carries such an override, check what it actually points at now that
it takes effect — it has never been applied. A per-channel `baseUrl` in the admin UI
overrides both, and is the better place for one channel's host.

```powershell
docker compose up -d --build          # run everything from source
docker compose ps                     # health
docker logs netzero-gateway-api-1     # api logs (or use Seq at :18082)
```

## Publishing images (Docker Hub `learnersight/…`, all public)

⚠️ **Gotcha**: `docker compose build` tags images as `netzero-gateway-*` (no namespace).
Pushing `learnersight/...` without retagging silently re-pushes the OLD image.
Always retag after building:

```powershell
docker compose build
docker tag netzero-gateway-api    learnersight/netzero-gateway-api:latest
docker tag netzero-gateway-web    learnersight/netzero-gateway-web:latest
docker tag netzero-gateway-backup learnersight/netzero-gateway-backup:latest
docker push learnersight/netzero-gateway-api:latest
docker push learnersight/netzero-gateway-web:latest
docker push learnersight/netzero-gateway-backup:latest
```

Docker Hub free plan: 1 private repo max, no collaborators on private repos —
that is why all three repos are public (client decision). Note the api image
contains production appsettings; see releases/1.0.0.md → Known limitations.

## Media Library

- Files live in the `mediadata` volume (`/app/media` inside the api container) —
  losing the volume breaks all shared links.
- **Confirmed safe against every redeploy path (hotfix batch 3, task 5).** Neither repo
  has a CI/CD pipeline — there is no `.github/workflows/` in either, and
  [testing.md](testing.md) already says so ("no CI"). The actual redeploy mechanism is
  the manual flow in [release-process.md](release-process.md): `docker compose build` →
  retag → push → client `docker compose pull && up -d`. `mediadata` is a genuine named
  Docker volume declared in `docker-compose.yml`, entirely outside both the `api` and
  `web` build contexts, so a rebuild or an image pull cannot touch it — only
  `docker compose down -v` or `docker volume rm mediadata` would, and neither is part of
  the documented ship flow. Nothing to change here; recorded so a future session doesn't
  re-investigate a CI/CD pipeline that was never built.
- `Media__PublicBaseUrl` (compose env on `api`) is what public links are built from;
  set it to the address message recipients can reach (the web service proxies
  `/media/` to the API, so the client's domain or `http://<server-ip>:18080`).
  **The shipped `deploy/docker-compose.yml` defaults to `http://localhost:18080`**, which
  uploads fine and then hands WhatsApp providers a link only the host itself can open —
  edit it before the first upload on a hosted server. The API logs a warning at every
  upload while the value is empty or names localhost. Links are built from the current
  value each time they are read (deep QA pass), so correcting it later fixes links to
  files uploaded earlier too; the `public_url` column is kept only as a record.
- **Hosted-server checklist** (deep QA pass; the local half is verified, the outside half
  needs the real host):
  1. `docker exec <api> id` shows `app`, and `ls -ld /app/media` shows `app app` — the
     Dockerfile creates and `chown`s the folder, and a new named volume inherits that
     ownership. A volume created earlier as root (or a bind mount) does not: fix with
     `docker exec -u root <api> chown -R app:app /app/media`.
  2. Inbound TCP 18080 is open on the host (e.g. `ufw allow 18080/tcp`) and any cloud
     firewall or security group.
  3. From a machine **outside** the host: upload a file in Media Library, then
     `curl -I <publicUrl>` returns `200` and the right `Content-Type` (HEAD works;
     it returned 405 before the QA pass), with no login.
  4. Send a WhatsApp media message to a test number using that exact link and confirm
     it arrives with the file. Inbox v1 has no media contract; use WaTrend or Inbox QR.
- The web proxy allows request lines up to 128k (`large_client_header_buffers`) so a
  `GET /sendtext` carrying hundreds of recipients is not answered with 414; Kestrel's
  limit is raised to match in `Program.cs`.
- Expired files are removed by the `media-cleanup` Hangfire job (Fridays 00:00
  container-local time).

## Client package

`deploy/` folder (workspace root) = pull-based compose + non-technical README.
Client only needs Docker Desktop → `docker compose pull` → `docker compose up -d`.

`web` waits on the API's healthcheck, not merely on its container starting. Without that
gate the login page is served while the API is still migrating and nginx answers `/api/*`
with 502, which the sign-in screen used to report as a failed sign-in — see
[releases/2.1.0.md](releases/2.1.0.md). Keep the `condition: service_healthy` form when
editing either compose file; the short `depends_on: [api]` list form does not wait.

Startup on a fresh database takes as long as migrations plus seeding; `docker compose ps`
shows `api` as `starting` until then, and the healthcheck allows 90s before it counts
failures.

## Backups

The `backup` service (Python) runs daily at 00:00 GMT+6 and writes next to the compose file:
- `netzero-db-backup.xlsx` — last 30 days (log tables filtered, config tables full)
- `backups/netzero-db-YYYY-MM-DD.dump` — pg_dump `-Fc`, pruned after 30 days

Restore: `docker exec -i netzero-gateway-db-1 pg_restore -U postgres -d NetZeroGatewayDB --clean <dump>`.
On-demand xlsx: admin UI toolbar (cloud icon) or `GET /api/backup/export?from=&to=`.

## Hosting minimums

2 vCPU / 4 GB RAM / 40 GB SSD Linux VPS with Docker (2 GB works without Seq).
Needs outbound HTTPS to providers, inbound 18080 (+18081 only for direct API integrations).
No TLS in-stack — put a reverse proxy with HTTPS in front for production.

## Voice host (hotfix batch 7)

The Voice provider moved from `api.sms.netzero.com.bd` to `obd.netzero.com.bd` (same server and
address, port 8000, `http`, paths unchanged; verified with a real call and the report). The code
default (`Providers:Voice:BaseUrl`) and the seeded channel now point at the new host. The host is
also the **Base URL** field on the Voice channel, so a future change is an admin edit in
Channels, not a deploy. **An existing channel keeps the Base URL it already stores**, because the
seed refreshes credentials only: on an installed system, open Channels, Voice, and set Base URL to
`http://obd.netzero.com.bd:8000`. Until then it keeps working, as both names reach the same server.
