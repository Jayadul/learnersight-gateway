# Architecture Decision Records

One page per load-bearing decision. Load an ADR only when questioning or revisiting
that decision — day-to-day rules already live in `docs/conventions.md` and the
CLAUDE.md non-negotiables.

| ADR | Decision |
|---|---|
| [0001](0001-clean-architecture-layering.md) | Clean Architecture layering |
| [0002](0002-convention-driven-authorization.md) | Authorization via `authorization.json`, not attributes |
| [0003](0003-snake-case-database-naming.md) | snake_case database naming |
| [0004](0004-jwt-lifetimes-and-localstorage.md) | JWT 15 min/7 days + localStorage tokens |
| [0005](0005-utc-storage-dhaka-display.md) | UTC storage, Dhaka display |
| [0006](0006-unify-whatsapp-channel-type.md) | WhatsApp text + media unified into one channel (`WP=1`, 2 retired) |
| [0007](0007-decouple-gateway-auth-from-channel-config.md) | Gateway `apikey`/`secretkey` moved off the SMS channel into `gateway_credentials` |
| [0008](0008-multi-provider-channels.md) | Several providers may serve one channel type; sender resolution keys on the provider |

## Adding one

Copy the format below into `NNNN-kebab-title.md`, keep it under ~20 lines, add a row
above. Write one when a decision is expensive to reverse, was debated, or a future
session might "helpfully" undo it.

```markdown
# ADR NNNN: Title
**Status**: Accepted (release/date) · **Decider**: team lead / client / engineering

## Context
## Decision
## Consequences
```
