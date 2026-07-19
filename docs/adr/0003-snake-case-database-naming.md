# ADR 0003: snake_case database naming
**Status**: Accepted (Release 1.0.0) · **Decider**: engineering

## Context
PostgreSQL convention is snake_case; C# entities are PascalCase. Quoted PascalCase
identifiers make psql work and external scripts painful.

## Decision
EFCore.NamingConventions rewrites all identifiers to snake_case automatically
(`MessageRequest` → `message_requests`).

## Consequences
- Zero per-entity mapping code; psql sessions read naturally.
- **Every** raw SQL consumer must use snake_case: the Python backup service, ad-hoc
  queries, future reporting. This bit us once in R1 (backup service) — the
  `db-migration` skill includes the impact scan.
