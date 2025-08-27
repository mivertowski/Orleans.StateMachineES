using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans.StateMachineES.Tracing;
using Orleans.StateMachineES.Visualization;

namespace Orleans.StateMachineES.Monitoring;

/// <summary>
/// REST API controller for state machine monitoring and health checks.
/// Provides endpoints for health status, metrics, and operational monitoring.
/// </summary>
[ApiController]
[Route("api/statemachine/monitoring")]
[Produces("application/json")]
public class StateMachineMonitoringController : ControllerBase
{
    private readonly IStateMachineHealthCheck _healthCheck;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<StateMachineMonitoringController> _logger;

    /// <summary>
    /// Initializes a new instance of the monitoring controller.
    /// </summary>
    /// <param name="healthCheck">The health check service.</param>
    /// <param name="grainFactory">Orleans grain factory.</param>
    /// <param name="logger">Logger instance.</param>
    public StateMachineMonitoringController(
        IStateMachineHealthCheck healthCheck,
        IGrainFactory grainFactory,
        ILogger<StateMachineMonitoringController> logger)
    {
        _healthCheck = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
        _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the overall health status of all monitored state machines.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>System health information.</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(SystemHealthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SystemHealthResult>> GetSystemHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _healthCheck.GetSystemHealthAsync(cancellationToken);
            
            // Set HTTP status based on health
            var statusCode = health.Status switch
            {
                HealthStatus.Healthy => StatusCodes.Status200OK,
                HealthStatus.Degraded => StatusCodes.Status200OK, // Still OK, but degraded
                HealthStatus.Unhealthy => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status200OK
            };

            return StatusCode(statusCode, health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system health");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve system health", message = ex.Message });
        }
    }

    /// <summary>
    /// Performs a health check on a specific state machine grain.
    /// </summary>
    /// <param name="grainType">The type of the grain.</param>
    /// <param name="grainId">The ID of the grain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result for the specific grain.</returns>
    [HttpGet("health/{grainType}/{grainId}")]
    [ProducesResponseType(typeof(StateMachineHealthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StateMachineHealthResult>> GetGrainHealthAsync(
        string grainType,
        string grainId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(grainType) || string.IsNullOrWhiteSpace(grainId))
        {
            return BadRequest(new { error = "Both grainType and grainId are required" });
        }

        try
        {
            var result = await _healthCheck.CheckHealthAsync(grainType, grainId, cancellationToken);
            
            var statusCode = result.Status switch
            {
                HealthStatus.Healthy => StatusCodes.Status200OK,
                HealthStatus.Degraded => StatusCodes.Status200OK,
                HealthStatus.Unhealthy => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status200OK
            };

            return StatusCode(statusCode, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check health for {GrainType}:{GrainId}", grainType, grainId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to perform health check", message = ex.Message });
        }
    }

    /// <summary>
    /// Performs batch health checks on multiple grains.
    /// </summary>
    /// <param name="request">Batch health check request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated health check results.</returns>
    [HttpPost("health/batch")]
    [ProducesResponseType(typeof(AggregatedHealthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AggregatedHealthResult>> BatchHealthCheckAsync(
        [FromBody] BatchHealthCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Grains == null || !request.Grains.Any())
        {
            return BadRequest(new { error = "At least one grain must be specified for batch health check" });
        }

        try
        {
            var grainChecks = request.Grains.Select(g => new GrainHealthCheck(
                g.GrainType,
                g.GrainId,
                request.TimeoutSeconds.HasValue ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value) : null
            ));

            var result = await _healthCheck.CheckHealthAsync(grainChecks, cancellationToken);
            
            var statusCode = result.OverallStatus switch
            {
                HealthStatus.Healthy => StatusCodes.Status200OK,
                HealthStatus.Degraded => StatusCodes.Status200OK,
                HealthStatus.Unhealthy => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status200OK
            };

            return StatusCode(statusCode, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform batch health check");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to perform batch health check", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets current system metrics for all state machines.
    /// </summary>
    /// <returns>Current metrics snapshot.</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<Dictionary<string, object>> GetMetrics()
    {
        try
        {
            var metrics = StateMachineMetrics.GetCurrentMetrics();
            
            // Add timestamp
            metrics["timestamp"] = DateTimeOffset.UtcNow;
            metrics["uptime"] = DateTimeOffset.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve metrics", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets detailed monitoring information for a specific grain type.
    /// </summary>
    /// <param name="grainType">The type of grain to monitor.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <returns>Monitoring information for the specified grain type.</returns>
    [HttpGet("grains/{grainType}")]
    [ProducesResponseType(typeof(GrainTypeMonitoringInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<GrainTypeMonitoringInfo>> GetGrainTypeMonitoringAsync(
        string grainType,
        [FromQuery] int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(grainType))
        {
            return Task.FromResult<ActionResult<GrainTypeMonitoringInfo>>(BadRequest(new { error = "GrainType is required" }));
        }

        if (limit <= 0 || limit > 1000)
        {
            return Task.FromResult<ActionResult<GrainTypeMonitoringInfo>>(BadRequest(new { error = "Limit must be between 1 and 1000" }));
        }

        try
        {
            // This would typically query a grain directory or registry
            // For now, we'll return basic monitoring info
            var info = new GrainTypeMonitoringInfo
            {
                GrainType = grainType,
                GeneratedAt = DateTime.UtcNow,
                TotalInstances = 0, // Would be populated from grain directory
                ActiveInstances = 0, // Would be populated from metrics
                AverageHealth = HealthStatus.Unknown,
                RecentActivities = new List<GrainActivity>()
            };

            return Task.FromResult<ActionResult<GrainTypeMonitoringInfo>>(Ok(info));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get monitoring info for grain type {GrainType}", grainType);
            return Task.FromResult<ActionResult<GrainTypeMonitoringInfo>>(StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve grain type monitoring info", message = ex.Message }));
        }
    }

    /// <summary>
    /// Gets system alerts and notifications.
    /// </summary>
    /// <param name="severity">Filter by alert severity.</param>
    /// <param name="limit">Maximum number of alerts to return.</param>
    /// <returns>List of current system alerts.</returns>
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(List<SystemAlert>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<SystemAlert>>> GetAlertsAsync(
        [FromQuery] AlertSeverity? severity = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var systemHealth = await _healthCheck.GetSystemHealthAsync();
            var alerts = systemHealth.Alerts;

            if (severity.HasValue)
            {
                alerts = alerts.Where(a => a.Severity == severity.Value).ToList();
            }

            alerts = alerts.OrderByDescending(a => a.CreatedAt)
                          .Take(Math.Min(limit, 100))
                          .ToList();

            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system alerts");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve system alerts", message = ex.Message });
        }
    }

    /// <summary>
    /// Triggers a manual health check refresh for the entire system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success confirmation.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> RefreshHealthChecksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Trigger a fresh health check by clearing cache
            // In a real implementation, you'd have a method to clear/refresh the cache
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await _healthCheck.GetSystemHealthAsync(cancellationToken);
                    _logger.LogInformation("Manual health check refresh completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh health checks");
                }
            }, cancellationToken);

            await Task.CompletedTask;
            return Accepted(new { message = "Health check refresh initiated", requestedAt = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate health check refresh");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to initiate health check refresh", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets a visualization report for a specific grain.
    /// </summary>
    /// <param name="grainType">The type of grain.</param>
    /// <param name="grainId">The ID of the grain.</param>
    /// <param name="format">The export format (json, dot, mermaid, etc.).</param>
    /// <returns>Visualization data in the requested format.</returns>
    [HttpGet("visualize/{grainType}/{grainId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult> GetGrainVisualizationAsync(
        string grainType,
        string grainId,
        [FromQuery] string format = "json")
    {
        if (string.IsNullOrWhiteSpace(grainType) || string.IsNullOrWhiteSpace(grainId))
        {
            return Task.FromResult<ActionResult>(BadRequest(new { error = "Both grainType and grainId are required" }));
        }

        try
        {
            // This would need to be implemented with proper grain type resolution
            // For now, return a placeholder response
            
            var visualizationData = new
            {
                grainType = grainType,
                grainId = grainId,
                format = format,
                generatedAt = DateTime.UtcNow,
                message = "Visualization endpoint - implementation would generate actual state machine diagram"
            };

            var contentType = format.ToLowerInvariant() switch
            {
                "json" => "application/json",
                "dot" => "text/vnd.graphviz",
                "mermaid" => "text/plain",
                "xml" => "application/xml",
                _ => "application/json"
            };

            return Task.FromResult<ActionResult>(Ok(visualizationData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate visualization for {GrainType}:{GrainId}", grainType, grainId);
            return Task.FromResult<ActionResult>(StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to generate visualization", message = ex.Message }));
        }
    }
}

/// <summary>
/// Request model for batch health checks.
/// </summary>
public class BatchHealthCheckRequest
{
    /// <summary>
    /// List of grains to check.
    /// </summary>
    public List<GrainIdentifier> Grains { get; set; } = new();

    /// <summary>
    /// Optional timeout in seconds for each health check.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// Identifies a grain for health checking.
/// </summary>
public class GrainIdentifier
{
    /// <summary>
    /// The type of the grain.
    /// </summary>
    public string GrainType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the grain.
    /// </summary>
    public string GrainId { get; set; } = string.Empty;
}

/// <summary>
/// Monitoring information for a specific grain type.
/// </summary>
public class GrainTypeMonitoringInfo
{
    /// <summary>
    /// The grain type name.
    /// </summary>
    public string GrainType { get; set; } = string.Empty;

    /// <summary>
    /// When this information was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Total number of grain instances ever created.
    /// </summary>
    public int TotalInstances { get; set; }

    /// <summary>
    /// Number of currently active instances.
    /// </summary>
    public int ActiveInstances { get; set; }

    /// <summary>
    /// Average health status across all instances.
    /// </summary>
    public HealthStatus AverageHealth { get; set; }

    /// <summary>
    /// Recent grain activities.
    /// </summary>
    public List<GrainActivity> RecentActivities { get; set; } = new();
}

/// <summary>
/// Represents a grain activity for monitoring.
/// </summary>
public class GrainActivity
{
    /// <summary>
    /// The grain ID.
    /// </summary>
    public string GrainId { get; set; } = string.Empty;

    /// <summary>
    /// Type of activity (activation, state_transition, etc.).
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// When the activity occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Additional details about the activity.
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}