# NetZeroGateway.API

The ASP.NET Core host. Wires DI, middleware, and exposes controllers.

## DI wiring

All registration is in `Extensions/ServiceCollectionExtensions.cs`:
- `AddControllersWithAuthPolicy()` — registers MVC + AuthorizationPolicyConvention
- `AddInfrastructure()` — EF Core, repositories, HTTP clients
- `AddApplicationServices()` — application-layer services
- `AddBackgroundJobs()` — Hangfire + Postgres storage; recurring jobs registered in Program.cs via `IRecurringJobManager`
- `AddJwtAuth()` — JWT Bearer authentication
- `AddCorsPolicy()` — CORS from config

## Authorization — convention-driven from authorization.json

All auth policies are defined in `authorization.json` and applied via `AuthorizationPolicyConvention`.
No `[Authorize]`, `[ApiExplorerSettings]`, or `[AllowAnonymous]` attributes on controllers.

The convention reads per-controller config from `Authorization:Controllers:{Name}`:
- `SwaggerGroup` — which Swagger doc group (default: "admin")
- `RequireAuth` — whether JWT auth is required (default: true)
- `Roles` — required roles array (default: none)

To change auth for a controller, edit authorization.json — not the controller file.

## Middleware order (defined in `Extensions/WebApplicationExtensions.cs`)

1. `GlobalExceptionMiddleware` — structured error responses
2. `ApiLoggingMiddleware` — request/response audit trail for /sendtext
3. Routing, CORS, Authentication, Authorization, MapControllers

## Swagger

Two doc groups, assigned by convention from authorization.json:
- `gateway` — SendText endpoint
- `admin` — all admin panel endpoints (default group)
