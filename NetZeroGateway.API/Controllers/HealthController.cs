using Microsoft.AspNetCore.Mvc;

namespace NetZeroGateway.API.Controllers;

/// <summary>
/// Readiness probe for the container healthcheck. Deliberately trivial: migrations and
/// seeding both run to completion before <c>app.Run()</c>, so the API answering this at
/// all already means the database is migrated and seeded. Anything heavier here would
/// only add a way for the probe to fail while the gateway is actually fine.
/// <para>
/// It exists so the web container can wait for readiness rather than for the API process
/// merely having started — nginx proxying to an API still running migrations returns 502,
/// which the admin sign-in screen shows as "Sign in failed. Please try again."
/// </para>
/// </summary>
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "healthy" });
}
