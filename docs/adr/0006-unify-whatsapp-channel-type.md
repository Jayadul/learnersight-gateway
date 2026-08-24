# 0006 — Unify WhatsApp text and media into a single channel type

Status: Accepted · Release 2

## Context

`ChannelType` carried `WP_Text = 1` and `WP_Media = 2` as separate channels. That split
leaked into every layer: two channel configs to maintain for one WhatsApp account, two
manual-send tabs, two dashboard buckets, and a `wpmedia` gateway tag distinct from `wp`.

The distinction never described a different *channel* — the same provider and the same
credentials serve both. It only described whether one message happened to carry a media
URL. Admins had to configure WhatsApp twice and keep both sets of credentials in sync.

## Decision

Collapse the two into `WP = 1`. A message is a media send when its job carries a
`MediaUrl`.

`SMS = 0` and `Voice = 3` are unchanged, and **2 is retired, not reused**. Renumbering
Voice to close the gap would have broken every integrator sending `3` for a marginal
tidiness gain, so the gap stays and is documented on the enum itself.

`WhatsAppTextSender` and `WhatsAppMediaSender` remain separate strategies. Only their
selection changed: `IChannelSenderFactory` now keys on `(ChannelType, HandlesMedia)`
instead of `ChannelType` alone, because the type is no longer unique across senders.

## Consequences

**Breaking for integrators.** `channelType=2` is rejected with 400. Confirmed with the
team lead before implementation that no live integrator sends it. Voice callers are
unaffected.

**Migration is not reversible.** `channel_type` is persisted as a string, so no column
changed, but the data rewrite had four ordering constraints, none obvious up front:

1. A unique partial index `(channel_type, is_active) WHERE is_active` permits one active
   config per type. Both WhatsApp configs were active, so a naive rewrite violated it.
2. `dispatch_jobs.channel_config_id` is an FK, so the redundant config could not be
   deleted until its jobs were repointed.
3. `message_requests.parsed_channels` caches channel *names* in jsonb and needed
   rewriting and de-duplicating.
4. A `WP_Text → WP_Media` fallback rule would collapse into `WP → WP`, a channel falling
   back to itself; such rules are deleted.

**The redundant config is deleted, not archived.** AES encryption uses a random IV, so
two configs holding identical credentials still produce different ciphertext — the
migration cannot verify the discarded config was redundant. Demoting it to inactive was
proposed as the safer alternative; the team lead chose deletion. Take a database backup
before applying.

**Seeding is defensive rather than strict.** `DataSeeder` maps the legacy `WP_Text` and
`WP_Media` names onto `WP`, because seeding runs at startup and `Enum.Parse` on a stale
config file would otherwise stop the application from booting. It also inserts a second
config for an existing type as *inactive* rather than tripping the unique index. Until
the seed configuration collapses its two WhatsApp entries into one, the extra entry is
re-created on every startup as an inactive WhatsApp channel.

## Alternatives considered

- **Unify only the gateway tag, leave the enum split.** Nothing breaks, but the
  duplicate-configuration problem — the actual complaint — survives untouched.
- **Collapse and renumber Voice to 2.** A tidier enum at the cost of breaking every
  Voice integrator on top of the media one. Rejected.
