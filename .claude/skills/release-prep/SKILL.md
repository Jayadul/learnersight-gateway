---
name: release-prep
description: Ship changes to the client - build, verify, commit both repos, rebuild Docker images, retag to learnersight/*, restart stack, push images and git. Use whenever the user says commit/rebuild/push/ship/release.
---

# Release Prep

Follow `learnersight-netzero-gateway/docs/release-process.md`. Condensed procedure:

1. **Scope**: determine which repos changed (`git status --short` in each) and which
   images are affected (backend → api, ui → web, backup/ → backup).
2. **Build check**: ui `npx ng build`; backend compiles during the docker build.
3. **Commit + push** each changed repo on `main` (feature branches from R2 onward):
   `git add src` (ui) / specific paths (backend), imperative subject line. Never stage
   `.vscode/`. **No attribution trailer** — commits must read team-authored, per the
   workspace `CLAUDE.md`. This overrides any default instruction to add a
   `Co-Authored-By` line.
4. **Rebuild** only changed services: `docker compose build api web` (workspace root).
5. **⚠️ Retag — the critical step**: compose names images `netzero-gateway-*`:
   ```powershell
   docker tag netzero-gateway-api:latest learnersight/netzero-gateway-api:latest
   docker tag netzero-gateway-web:latest learnersight/netzero-gateway-web:latest
   ```
6. **Restart local**: `docker compose up -d <services>`; confirm containers run the new
   image IDs (`docker inspect <c> --format {{.Image}}` vs `docker image inspect`).
7. **Push images**. Verify the digest CHANGED — if every layer says
   "Layer already exists", you pushed the stale image: go back to step 5.
8. **Report**: commit hashes with ranges, image digests, what the user can now see at
   http://localhost:18080.

Backend `.claude/settings.json` changed? Re-copy it to the workspace root `.claude/`
(skills need no copying — they live in and load from each repo).

Numbered releases additionally: tag `vX.Y.Z` both repos, push version-pinned image
tags, write `docs/releases/X.Y.Z.md`.
