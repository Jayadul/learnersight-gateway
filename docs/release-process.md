# Release Process

Load this when: shipping any change to the client (code → images → git), or cutting a release.

## Ship checklist (every deliverable change)

1. **Build & verify locally**
   - Frontend: `npx ng build` must pass; backend: `dotnet build` (use `-o <scratch>` if
     Visual Studio locks bin/).
   - Run the smoke test in [testing.md](testing.md).
2. **Commit + push git** (both repos if both changed; backend first). See [git.md](git.md).
3. **Rebuild images**: `docker compose build api web` (or just the changed one;
   `backup` only when `backup/` changed).
4. **⚠️ Retag** — compose names images `netzero-gateway-*`; you MUST retag to
   `learnersight/*:latest` before pushing (see [deployment.md](deployment.md)).
5. **Restart local stack**: `docker compose up -d api web` and re-verify.
6. **Push images** to Docker Hub.
7. **Confirm**: pushed digest changed (an all-`Layer already exists` push means you
   pushed the stale image — go back to step 4).

## Cutting a numbered release

1. Ship checklist above, green.
2. Tag both repos `vX.Y.Z`, push tags.
3. Also push version-pinned images:
   `docker tag learnersight/netzero-gateway-api:latest learnersight/netzero-gateway-api:X.Y.Z` (× 3) and push.
4. Write `docs/releases/X.Y.Z.md` — use `1.0.0.md` as the template (features, limitations,
   debt, lessons).
5. Tell the client to run `docker compose pull && docker compose up -d` in their
   `deploy/` folder.

## Rollback

- Client side: images are the unit of rollback. Re-push the previous version-pinned
  images as `:latest` (or edit the client compose to the pinned tag), then
  `docker compose pull && up -d`.
- Database: restore the newest `backups/netzero-db-*.dump` (see deployment.md).
  Migrations are forward-only — never roll back schema in place.
