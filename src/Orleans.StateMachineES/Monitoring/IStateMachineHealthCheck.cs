namespace Orleans.StateMachineES.Monitoring;

/// <summary>
/// Interface for state machine health checks.
/// </summary>
public interface IStateMachineHealthCheck
{
    /// <summary>
    /// Performs a health check on the specified state machine grain.
    /// </summary>
    /// <param name="grainType">Type of the grain to check.</param>
    /// <param name="grainId">ID of the grain to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result.</returns>
    Task<StateMachineHealthResult> CheckHealthAsync(
        string grainType,
        string grainId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a health check on multiple state machine grains.
    /// </summary>
    /// <param name="grainChecks">List of grains to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated health check results.</returns>
    Task<AggregatedHealthResult> CheckHealthAsync(
        IEnumerable<GrainHealthCheck> grainChecks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overall system health for all monitored state machines.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>System-wide health status.</returns>
    Task<SystemHealthResult> GetSystemHealthAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a grain to be health checked.
/// </summary>
public record GrainHealthCheck(string GrainType, string GrainId, TimeSpan? Timeout = null);

/// <summary>
/// Result of a state machine health check.
/// </summary>
public class StateMachineHealthResult
{
    public string GrainType { get; set; } = string.Empty;
    public string GrainId { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string? CurrentState { get; set; }
    public TimeSpan CheckDuration { get; set; }
    public DateTime CheckedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];

    public bool IsHealthy => Status == HealthStatus.Healthy;
    public bool IsUnhealthy => Status == HealthStatus.Unhealthy;
    public bool IsDegraded => Status == HealthStatus.Degraded;
}

/// <summary>
/// Aggregated health results for multiple grains.
/// </summary>
public class AggregatedHealthResult
{
    public HealthStatus OverallStatus { get; set; }
    public int TotalGrains { get; set; }
    public int HealthyGrains { get; set; }
    public int UnhealthyGrains { get; set; }
    public int DegradedGrains { get; set; }
    public TimeSpan TotalCheckDuration { get; set; }
    public DateTime CheckedAt { get; set; }
    public List<StateMachineHealthResult> Results { get; set; } = [];

    public double HealthPercentage => TotalGrains > 0 ? (double)HealthyGrains / TotalGrains * 100 : 0;
}

/// <summary>
/// System-wide health information.
/// </summary>
public class SystemHealthResult
{
    public HealthStatus Status { get; set; }
    public int TotalMonitoredGrains { get; set; }
    public Dictionary<string, int> GrainTypesCounts { get; set; } = [];
    public Dictionary<HealthStatus, int> StatusDistribution { get; set; } = [];
    public DateTime LastUpdated { get; set; }
    public SystemMetrics Metrics { get; set; } = new();
    public List<SystemAlert> Alerts { get; set; } = [];
}

/// <summary>
/// System metrics for monitoring.
/// </summary>
public class SystemMetrics
{
    public long TotalStateTransitions { get; set; }
    public double AverageTransitionTime { get; set; }
    public long TotalErrors { get; set; }
    public double ErrorRate { get; set; }
    public int ActiveSagas { get; set; }
    public TimeSpan Uptime { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; } = [];
}

/// <summary>
/// System alert information.
/// </summary>
public class SystemAlert
{
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Details { get; set; } = [];
}

/// <summary>
/// Health status enumeration.
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}