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
Token to the same seed block, for the same reason as the others: a value set only
through the admin API is wiped on the next restart by the seeder's config refresh (see
architecture.md). Same rotation caveat applies before this carries production traffic.

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
  Changing it only affects files uploaded afterwards.
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
