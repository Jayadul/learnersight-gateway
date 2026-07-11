---
name: backend-endpoint
description: Add or modify a .NET API endpoint following NetZeroGateway conventions - authorization.json, Clean Architecture layering, DTOs, DI extensions, snake_case EF. Use for any backend feature work.
---

# Backend Endpoint

Load first: `learnersight-netzero-gateway/docs/architecture.md` + `docs/api.md`.

## Recipe

1. **DTOs** in `NetZeroGateway.Application/DTOs/` (request + response classes).
2. **Service**: interface + implementation in `Application/Services/`; repository
   interfaces in `Application/Interfaces/`, implemented in `Infrastructure`.
   Application must NOT reference Infrastructure.
3. **Controller** in `NetZeroGateway.API/Controllers/` — thin, kebab-case route
   (`[Route("api/thing-things")]`), returns `Ok(await service...)`.
   **NO `[Authorize]` / `[AllowAnonymous]` / `[ApiExplorerSettings]` attributes.**
4. **Authorization**: add/extend the controller entry in
   `NetZeroGateway.API/authorization.json` (`RequireAuth`, `Roles: ["SuperAdmin"]`
   for admin-only). This is the ONLY place auth is configured.
5. **DI**: register in `API/Extensions/ServiceCollectionExtensions.cs`
   (`AddApplicationServices` / `AddInfrastructure`).
6. **Errors**: throw `NotFoundException` / `UnauthorizedException` /
   `InvalidOperationException` with **customer-readable messages** — the middleware
   maps them to ProblemDetails and the UI shows `detail` verbatim.
7. **EF**: entities in Domain; snake_case is automatic — but any raw SQL must use
   snake_case names. New entity → migration
   (`dotnet ef migrations add <N> -p NetZeroGateway.Infrastructure -s NetZeroGateway.API`);
   seeding in `DataSeeder` must stay idempotent.
8. **Enums serialize as integers** — tell the UI the numeric values.

## Verify

`dotnet build` (use `-o <scratch>` if VS locks bin/), then hit the endpoint per
`docs/testing.md`. Update `docs/api.md` controller map if you added a controller.
