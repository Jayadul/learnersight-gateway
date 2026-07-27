# ADR 0002: Authorization via authorization.json, not attributes
**Status**: Accepted (Release 1.0.0) · **Decider**: engineering

## Context
Attribute-based auth scatters policy across controllers and makes the role matrix
impossible to review in one place.

## Decision
All endpoint authorization is declared in `NetZeroGateway.API/authorization.json`
(per-controller `RequireAuth` + `Roles`), enforced by convention at startup.
`[Authorize]` / `[AllowAnonymous]` attributes are banned on controllers.

## Consequences
- The whole role matrix (SuperAdmin vs Admin) is one reviewable file.
- A new controller without an entry is a bug — the `backend-endpoint` skill makes
  the entry a mandatory step.
- The file also carries the `Providers` base URLs (pragmatic co-location; revisit if
  it grows).
