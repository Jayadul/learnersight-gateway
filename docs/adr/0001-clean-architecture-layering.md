# ADR 0001: Clean Architecture layering
**Status**: Accepted (Release 1.0.0) · **Decider**: engineering

## Context
Multi-channel gateway with three provider integrations that will grow; provider SDKs
and EF must stay swappable and out of business logic.

## Decision
Domain ← Application ← Infrastructure, with `NetZeroGateway.API` as the composition
root. Application defines repository/provider interfaces; Infrastructure implements
them. Application must never reference Infrastructure.

## Consequences
- New features cost one extra interface hop but stay provider-agnostic.
- DI registration is centralized in `API/Extensions/ServiceCollectionExtensions.cs`.
- Held throughout R1 with no shortcut references — keep it that way; a violation is
  a build-review finding, not a style nit.
