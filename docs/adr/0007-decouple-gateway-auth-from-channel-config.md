# ADR 0007: Gateway authentication decoupled from channel config
**Status**: Accepted (Release 2.1 / 2026-09-02) · **Decider**: team lead

## Context
`/sendtext` validated `apikey`/`secretkey` against the **default SMS `ChannelConfig`** —
the same encrypted blob that holds the SMS provider's own credentials. Release 2 made
several channels of a type active at once, so "the SMS channel" stopped being a single
thing and gateway access became a side effect of whichever config happened to carry
`is_default`. Two consequences neither party ever agreed to: promoting a different SMS
channel to default silently revoked every integrator's access, and rotating a provider
account's keys did the same. It also meant the credentials handed to a third party were
the provider's, not ours.

## Decision
Gateway credentials live in their own single-row `gateway_credentials` table —
`username`, `api_key_encrypted`, `secret_key_encrypted`, the two keys AES-encrypted with
`IEncryptionService` exactly as `channel_configs.config_json` is. `SendTextOrchestratorService`
validates against that row and nothing else. The admin UI edits them at Configuration →
Gateway Credentials (SuperAdmin), which reads both keys back **decrypted** — a SuperAdmin
has to hand the exact pair to an integrator and the column is the only copy, so masking
them the way a channel config is masked would make the screen unable to do its job. Both
fields sit behind a reveal toggle so the values are available on purpose rather than on
display. Every field of the update is optional so changing one cannot blank the others. `username` is recorded for
the admin panel only: callers do not send it, so renaming it cannot break an integration.
`DataSeeder` creates the row from `Seed:GatewayCredentials` on a fresh database and then
never touches it again — re-asserting would roll back a rotation on the next restart.

## Consequences
Credentials that worked before 2.1 stop working: the old ones were an SMS provider's, and
they no longer authenticate anything. This is deliberate and bundled with the 2.1 tag
grammar break, which already forces every integrator to change their templates — one
migration for both rather than two. Integrators must be given the new username/apikey/
secretkey before 2.1 is deployed.

Channel and gateway credentials are now rotated independently, which is the point, but it
does mean two places to look when a caller reports `101 REJECTD`: the gateway credentials
for an auth rejection, the channel config for a provider-side failure. The `Reason` field
distinguishes them — auth rejections carry none.
