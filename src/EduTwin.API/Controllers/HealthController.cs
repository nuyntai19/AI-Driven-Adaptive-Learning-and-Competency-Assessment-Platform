using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EduTwin.API.Controllers;

/// <summary>
/// Health check endpoints per API_CONTRACTS.md §63, 64.
/// </summary>
[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Liveness probe — always returns Healthy.
    /// Does not check any dependency.
    /// </summary>
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "Healthy" });
    }

    /// <summary>
    /// Readiness probe — checks MySQL availability.
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
        
        var isHealthy = report.Status == HealthStatus.Healthy;
        var result = new
        {
            status = isHealthy ? "Healthy" : "Unhealthy",
            checks = new
            {
                mysql = report.Entries.TryGetValue("mysql", out var mysqlEntry) 
                    ? (mysqlEntry.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy")
                    : "Unhealthy"
            }
        };

        if (isHealthy)
        {
            return Ok(result);
        }

        return StatusCode(503, result);
    }
}
