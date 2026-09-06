# 0008 — Several providers may serve one channel type

Status: Accepted · Release 3.0

## Context

Until Release 3 a channel type implied its vendor: SMS meant REVE, WhatsApp meant
WaTrend, Voice meant NetZero. Nothing in the system had to name a provider, and two
places quietly hard-wired that assumption.

`IChannelSenderFactory` resolved a sender from `(ChannelType, HandlesMedia)` (ADR 0006).
More restrictive still, each provider's base URL was bound once at DI registration —
one `HttpClient` per channel type, `BaseAddress` taken from `Providers:*:BaseUrl`. Two
SMS vendors could not coexist under that topology no matter what the factory did.

`ChannelConfig.ProviderName` existed, but as free text read by nothing.

Release 3 adds GenNet and Chithi for SMS and NetZero Inbox for WhatsApp. Two providers
now serve one type, and picking the wrong one does not fail loudly — it sends the
message through the wrong vendor and bills the wrong account.

## Decision

**The provider is part of the sender-resolution key.** `Provider` becomes an enum on
`ChannelConfig`, `IChannelSender` declares which provider it talks to, and the factory
keys on `(ChannelType, Provider, HandlesMedia)`.

**No substitution on a miss.** A channel naming a provider with no registered sender
throws. Falling back to another vendor of the same type would route live traffic — and
cost — somewhere the operator never asked for. The one fallback kept is a media job on a
provider that registers a single sender, which is a shape question, not a vendor one.

**Duplicate keys fail at startup**, not at dispatch: two senders claiming one key would
otherwise resolve by DI registration order.

**Base URLs move to the channel config**, falling back to the central
`Providers:*:BaseUrl` when blank — which is every channel that existed before R3. The
admin UI has collected a `baseUrl` field per channel since it shipped and nothing read
it; this makes that field real.

**`NetZeroVoice` and `NetZeroInbox` are spelled out in full.** They are unrelated
services that happen to share a brand, and the product is also called NetZero. The
seeder refuses to guess from a bare `"netzero"`.

`ProviderName` survives as display text, so an installation can label two REVE channels
distinctly. Dispatch never reads it.

## Consequences

**Not breaking for integrators.** The `/sendtext` contract is unchanged — a tag names a
channel label, and the label already implied a config. Which vendor that config uses is
an admin concern.

**Existing rows are backfilled by migration**, mapping channel type to its sole former
provider. The enum persists as text and the generated default was `''`, which parses
back to nothing — without the backfill every pre-R3 channel becomes unreadable on the
first query. Seed files need no edit: `ParseProvider` matches `ProviderName`'s text and
falls back to the channel type.

**A provider that cannot serve a channel type is rejected at save time**
(`ChannelConfigService.ValidateProvider`) rather than at dispatch, when there is a real
message to lose.

**Adding a vendor is now additive**: implement `IChannelSender`, declare its provider,
register it. No existing sender changes.
