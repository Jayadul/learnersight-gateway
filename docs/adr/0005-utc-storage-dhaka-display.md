# ADR 0005: UTC storage, Dhaka display
**Status**: Accepted (Release 1.0.0) · **Decider**: engineering + client (GMT+6 ops)

## Context
Bangladesh-first deployment (backups and reporting expected in GMT+6), but servers,
Docker containers, and Postgres default to UTC.

## Decision
Store and process all timestamps in UTC. Convert at the edges via `AppTime` /
`App:TimeZone` (Dhaka, UTC+6). Background jobs are *scheduled* in Dhaka time
(e.g. backups at 00:00 GMT+6).

## Consequences
- One conversion seam; no scattered `DateTime.Now`.
- New code must go through `AppTime` — full standard in `docs/conventions.md`.
- If a second client in another timezone appears, `App:TimeZone` is per-deployment
  config, not a code change.
