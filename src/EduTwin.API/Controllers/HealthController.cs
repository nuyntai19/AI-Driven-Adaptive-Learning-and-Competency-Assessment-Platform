using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

/// <summary>
/// Health check endpoints per API_CONTRACTS.md §63.
/// Anonymous access, no dependency checks for /live.
/// </summary>
[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Liveness probe — always returns Healthy.
    /// Does not check any dependency.
    /// </summary>
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "Healthy" });
    }
}
