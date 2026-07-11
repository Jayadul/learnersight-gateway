# Deployment & Operations

Load this when: building/pushing images, running the stack, debugging containers, or backups.

## Stack (compose at workspace root, one level above this repo)

| Service | Image | Port | Notes |
|---|---|---|---|
| db | postgres:16-alpine | 15432 | volume `pgdata` |
| seq | datalust/seq | 18082 | log viewer, volume `seqdata` |
| api | built from this repo | 18081 | migrations+seed on startup |
| web | built from `../learnersight-netzero-gateway-ui` | 18080 | nginx serves SPA, proxies `/api/` and `/sendtext` |
| backup | built from `../backup` | — | nightly exports, writes next to compose file |

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

## Client package

`deploy/` folder (workspace root) = pull-based compose + non-technical README.
Client only needs Docker Desktop → `docker compose pull` → `docker compose up -d`.

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
