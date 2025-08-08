using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Orleans.StateMachineES.Monitoring;

/// <summary>
/// Extensions for integrating state machine health checks with ASP.NET Core health checks.
/// </summary>
public static class AspNetCoreHealthCheckExtensions
{
    /// <summary>
    /// Adds state machine health checks to the ASP.NET Core health check system.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="healthCheckName">Name for the health check.</param>
    /// <param name="configureOptions">Configuration for health check options.</param>
    /// <param name="tags">Tags for the health check.</param>
    /// <param name="timeout">Timeout for the health check.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateMachineHealthCheck(
        this IServiceCollection services,
        string healthCheckName = "statemachine",
        Action<StateMachineHealthCheckOptions>? configureOptions = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        // Register the health check service
        services.AddSingleton<IStateMachineHealthCheck, StateMachineHealthCheckService>();
        
        // Configure options
        var options = new StateMachineHealthCheckOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // Add to ASP.NET Core health checks
        services.AddHealthChecks()
            .AddCheck<StateMachineHealthCheckIntegration>(
                healthCheckName,
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: tags,
                timeout: timeout ?? TimeSpan.FromSeconds(30));

        return services;
    }

    /// <summary>
    /// Adds specific grain health checks to the ASP.NET Core health check system.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="grainChecks">Specific grains to monitor.</param>
    /// <param name="healthCheckName">Name for the health check.</param>
    /// <param name="tags">Tags for the health check.</param>
    /// <param name="timeout">Timeout for the health check.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateMachineGrainHealthChecks(
        this IServiceCollection services,
        IEnumerable<GrainHealthCheck> grainChecks,
        string healthCheckName = "statemachine_grains",
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        services.AddSingleton(new GrainHealthCheckConfiguration(grainChecks));

        services.AddHealthChecks()
            .AddCheck<StateMachineGrainHealthCheckIntegration>(
                healthCheckName,
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: tags,
                timeout: timeout ?? TimeSpan.FromSeconds(60));

        return services;
    }
}

/// <summary>
/// ASP.NET Core health check integration for state machines.
/// </summary>
public class StateMachineHealthCheckIntegration : IHealthCheck
{
    private readonly IStateMachineHealthCheck _healthCheck;
    private readonly ILogger<StateMachineHealthCheckIntegration> _logger;

    /// <summary>
    /// Initializes a new instance of the health check integration.
    /// </summary>
    /// <param name="healthCheck">State machine health check service.</param>
    /// <param name="logger">Logger instance.</param>
    public StateMachineHealthCheckIntegration(
        IStateMachineHealthCheck healthCheck,
        ILogger<StateMachineHealthCheckIntegration> logger)
    {
        _healthCheck = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var systemHealth = await _healthCheck.GetSystemHealthAsync(cancellationToken);

            var healthCheckStatus = systemHealth.Status switch
            {
                Monitoring.HealthStatus.Healthy => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
                Monitoring.HealthStatus.Degraded => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                Monitoring.HealthStatus.Unhealthy => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                _ => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy
            };

            var data = new Dictionary<string, object>
            {
                ["total_monitored_grains"] = systemHealth.TotalMonitoredGrains,
                ["status_distribution"] = systemHealth.StatusDistribution,
                ["grain_types"] = systemHealth.GrainTypesCounts,
                ["last_updated"] = systemHealth.LastUpdated,
                ["alerts_count"] = systemHealth.Alerts.Count,
                ["error_rate"] = systemHealth.Metrics.ErrorRate,
                ["active_sagas"] = systemHealth.Metrics.ActiveSagas
            };

            var description = healthCheckStatus switch
            {
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => 
                    $"All {systemHealth.TotalMonitoredGrains} state machine grains are healthy",
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => 
                    $"Some state machine grains are degraded. {systemHealth.Alerts.Count} alerts active",
                _ => 
                    $"State machine system is unhealthy. {systemHealth.Alerts.Count} alerts, error rate: {systemHealth.Metrics.ErrorRate:P2}"
            };

            return new HealthCheckResult(healthCheckStatus, description, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "State machine health check failed");
            
            return new HealthCheckResult(
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                "State machine health check failed with exception",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["exception_type"] = ex.GetType().Name
                });
        }
    }
}

/// <summary>
/// ASP.NET Core health check integration for specific grains.
/// </summary>
public class StateMachineGrainHealthCheckIntegration : IHealthCheck
{
    private readonly IStateMachineHealthCheck _healthCheck;
    private readonly GrainHealthCheckConfiguration _configuration;
    private readonly ILogger<StateMachineGrainHealthCheckIntegration> _logger;

    /// <summary>
    /// Initializes a new instance of the grain health check integration.
    /// </summary>
    /// <param name="healthCheck">State machine health check service.</param>
    /// <param name="configuration">Grain health check configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public StateMachineGrainHealthCheckIntegration(
        IStateMachineHealthCheck healthCheck,
        GrainHealthCheckConfiguration configuration,
        ILogger<StateMachineGrainHealthCheckIntegration> logger)
    {
        _healthCheck = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var aggregatedResult = await _healthCheck.CheckHealthAsync(_configuration.GrainChecks, cancellationToken);

            var healthCheckStatus = aggregatedResult.OverallStatus switch
            {
                Monitoring.HealthStatus.Healthy => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
                Monitoring.HealthStatus.Degraded => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                Monitoring.HealthStatus.Unhealthy => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                _ => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy
            };

            var data = new Dictionary<string, object>
            {
                ["total_grains"] = aggregatedResult.TotalGrains,
                ["healthy_grains"] = aggregatedResult.HealthyGrains,
                ["unhealthy_grains"] = aggregatedResult.UnhealthyGrains,
                ["degraded_grains"] = aggregatedResult.DegradedGrains,
                ["health_percentage"] = aggregatedResult.HealthPercentage,
                ["total_check_duration_ms"] = aggregatedResult.TotalCheckDuration.TotalMilliseconds,
                ["individual_results"] = aggregatedResult.Results.ToDictionary(
                    r => $"{r.GrainType}:{r.GrainId}",
                    r => new
                    {
                        status = r.Status.ToString(),
                        current_state = r.CurrentState,
                        duration_ms = r.CheckDuration.TotalMilliseconds,
                        error = r.ErrorMessage
                    })
            };

            var description = healthCheckStatus switch
            {
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => 
                    $"All {aggregatedResult.TotalGrains} specified grains are healthy ({aggregatedResult.HealthPercentage:F1}%)",
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => 
                    $"{aggregatedResult.HealthyGrains}/{aggregatedResult.TotalGrains} grains healthy, {aggregatedResult.DegradedGrains} degraded",
                _ => 
                    $"{aggregatedResult.UnhealthyGrains}/{aggregatedResult.TotalGrains} grains unhealthy, health: {aggregatedResult.HealthPercentage:F1}%"
            };

            return new HealthCheckResult(healthCheckStatus, description, data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Grain health check failed");
            
            return new HealthCheckResult(
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                "Grain health check failed with exception",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["exception_type"] = ex.GetType().Name,
                    ["configured_grains"] = _configuration.GrainChecks.Count()
                });
        }
    }
}

/// <summary>
/// Configuration for grain-specific health checks.
/// </summary>
public class GrainHealthCheckConfiguration
{
    /// <summary>
    /// The grains to monitor.
    /// </summary>
    public IEnumerable<GrainHealthCheck> GrainChecks { get; }

    /// <summary>
    /// Initializes a new instance of the configuration.
    /// </summary>
    /// <param name="grainChecks">The grains to monitor.</param>
    public GrainHealthCheckConfiguration(IEnumerable<GrainHealthCheck> grainChecks)
    {
        GrainChecks = grainChecks ?? throw new ArgumentNullException(nameof(grainChecks));
    }
}