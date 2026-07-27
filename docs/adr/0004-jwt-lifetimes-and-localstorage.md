# ADR 0004: JWT 15 min / 7 days + localStorage tokens
**Status**: Accepted (Release 1.0.0) · **Decider**: client (red line)

## Context
Client mandated short access tokens with a 7-day refresh window; the Angular app
needs persistence across tabs/restarts without a backend session store.

## Decision
Access token 15 minutes, refresh token 7 days. Both persisted in localStorage;
the HTTP interceptor performs single-flight refresh.

## Consequences
- localStorage is XSS-readable — an explicitly **client-accepted** tradeoff.
- Do not re-propose longer lifetimes, cookies/httpOnly storage, or "remember me"
  variants; the client has rejected changes here.
- Any future XSS finding is high-severity because of this decision.
