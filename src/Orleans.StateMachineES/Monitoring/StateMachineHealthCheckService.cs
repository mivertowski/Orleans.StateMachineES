using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Tracing;

namespace Orleans.StateMachineES.Monitoring;

/// <summary>
/// Service for performing health checks on state machine grains.
/// </summary>
public class StateMachineHealthCheckService : IStateMachineHealthCheck
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<StateMachineHealthCheckService> _logger;
    private readonly StateMachineHealthCheckOptions _options;
    private readonly ConcurrentDictionary<string, StateMachineHealthResult> _lastResults = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastChecked = new();
    private SystemHealthResult? _lastSystemHealth;
    private DateTime _lastSystemHealthUpdate = DateTime.MinValue;

    public StateMachineHealthCheckService(
        IGrainFactory grainFactory,
        ILogger<StateMachineHealthCheckService> logger,
        StateMachineHealthCheckOptions? options = null)
    {
        _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new StateMachineHealthCheckOptions();
    }

    /// <inheritdoc />
    public async Task<StateMachineHealthResult> CheckHealthAsync(
        string grainType,
        string grainId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{grainType}:{grainId}";
        var now = DateTime.UtcNow;

        // Check cache if enabled
        if (_options.EnableCaching &&
            _lastResults.TryGetValue(cacheKey, out var cachedResult) &&
            _lastChecked.TryGetValue(cacheKey, out var lastCheck) &&
            now - lastCheck < _options.CacheDuration)
        {
            _logger.LogDebug("Returning cached health result for {GrainType}:{GrainId}", grainType, grainId);
            return cachedResult;
        }

        var result = new StateMachineHealthResult
        {
            GrainType = grainType,
            GrainId = grainId,
            CheckedAt = now
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use tracing for health check operations
            var healthResult = await TracingHelper.TraceHealthCheck(
                grainType: grainType,
                grainId: grainId,
                operation: async () =>
                {
                    var grain = GetGrain(grainType, grainId);
                    return await PerformHealthCheckAsync(grain, cancellationToken);
                });

            result.Status = healthResult.Status;
            result.CurrentState = healthResult.CurrentState;
            result.Metadata = healthResult.Metadata;

            _logger.LogDebug("Health check completed for {GrainType}:{GrainId} with status {Status}",
                grainType, grainId, result.Status);
        }
        catch (OperationCanceledException)
        {
            result.Status = HealthStatus.Unknown;
            result.ErrorMessage = "Health check was cancelled";
            _logger.LogWarning("Health check cancelled for {GrainType}:{GrainId}", grainType, grainId);
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            
            _logger.LogError(ex, "Health check failed for {GrainType}:{GrainId}", grainType, grainId);
        }
        finally
        {
            stopwatch.Stop();
            result.CheckDuration = stopwatch.Elapsed;
        }

        // Update cache
        if (_options.EnableCaching)
        {
            _lastResults[cacheKey] = result;
            _lastChecked[cacheKey] = now;
        }

        // Update metrics
        StateMachineMetrics.RecordHealthCheck(result.Status.ToString(), result.CheckDuration);

        return result;
    }

    /// <inheritdoc />
    public async Task<AggregatedHealthResult> CheckHealthAsync(
        IEnumerable<GrainHealthCheck> grainChecks,
        CancellationToken cancellationToken = default)
    {
        var checks = grainChecks.ToList();
        var result = new AggregatedHealthResult
        {
            TotalGrains = checks.Count,
            CheckedAt = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute health checks in parallel with controlled concurrency
            var semaphore = new SemaphoreSlim(_options.MaxConcurrentChecks);
            var tasks = checks.Select(async check =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    using var timeoutCts = new CancellationTokenSource(check.Timeout ?? _options.DefaultTimeout);
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, timeoutCts.Token);

                    return await CheckHealthAsync(check.GrainType, check.GrainId, combinedCts.Token);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var healthResults = await Task.WhenAll(tasks);
            result.Results = healthResults.ToList();

            // Calculate aggregate statistics
            result.HealthyGrains = healthResults.Count(r => r.Status == HealthStatus.Healthy);
            result.UnhealthyGrains = healthResults.Count(r => r.Status == HealthStatus.Unhealthy);
            result.DegradedGrains = healthResults.Count(r => r.Status == HealthStatus.Degraded);

            // Determine overall status
            result.OverallStatus = DetermineOverallHealth(healthResults);

            _logger.LogInformation(
                "Batch health check completed: {Total} grains, {Healthy} healthy, {Unhealthy} unhealthy, {Degraded} degraded",
                result.TotalGrains, result.HealthyGrains, result.UnhealthyGrains, result.DegradedGrains);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch health check failed");
            result.OverallStatus = HealthStatus.Unknown;
        }
        finally
        {
            stopwatch.Stop();
            result.TotalCheckDuration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SystemHealthResult> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Return cached result if still valid
        if (_lastSystemHealth != null &&
            now - _lastSystemHealthUpdate < _options.SystemHealthCacheDuration)
        {
            return _lastSystemHealth;
        }

        var result = new SystemHealthResult
        {
            LastUpdated = now,
            TotalMonitoredGrains = _lastResults.Count
        };

        try
        {
            // Analyze cached results
            var recentResults = _lastResults.Values
                .Where(r => now - r.CheckedAt < _options.SystemHealthWindow)
                .ToList();

            // Calculate grain type distribution
            result.GrainTypesCounts = recentResults
                .GroupBy(r => r.GrainType)
                .ToDictionary(g => g.Key, g => g.Count());

            // Calculate status distribution
            result.StatusDistribution = recentResults
                .GroupBy(r => r.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            // Determine overall system status
            result.Status = DetermineSystemHealth(recentResults);

            // Calculate system metrics
            result.Metrics = await CalculateSystemMetricsAsync();

            // Generate alerts
            result.Alerts = GenerateSystemAlerts(recentResults, result.Metrics);

            _logger.LogDebug("System health calculated: {Status}, {TotalGrains} monitored grains",
                result.Status, result.TotalMonitoredGrains);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate system health");
            result.Status = HealthStatus.Unknown;
        }

        // Cache the result
        _lastSystemHealth = result;
        _lastSystemHealthUpdate = now;

        return result;
    }

    private IGrainWithStringKey GetGrain(string grainType, string grainId)
    {
        // This is a simplified approach - in a real implementation, you'd need a grain factory
        // that can create grains based on type name. This could be done with reflection or
        // a registered factory pattern.
        
        // For now, we'll assume the grain implements IStateMachineGrain interface
        // In a full implementation, you'd have a grain registry or use Orleans' grain directory
        return _grainFactory.GetGrain<IGrainWithStringKey>(grainId);
    }

    private async Task<(HealthStatus Status, string? CurrentState, Dictionary<string, object> Metadata)> 
        PerformHealthCheckAsync(IGrainWithStringKey grain, CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, object>();

        try
        {
            // Basic connectivity check
            var pingTask = PingGrainAsync(grain);
            var timeoutTask = Task.Delay(_options.DefaultTimeout, cancellationToken);
            
            var completedTask = await Task.WhenAny(pingTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                return (HealthStatus.Unhealthy, null, metadata.With("error", "Timeout"));
            }

            var isResponsive = await pingTask;
            if (!isResponsive)
            {
                return (HealthStatus.Unhealthy, null, metadata.With("error", "Grain not responsive"));
            }

            // If grain implements IStateMachineGrain, get additional info
            if (grain is IStateMachineGrain<Enum, Enum> stateMachineGrain)
            {
                var currentState = await GetCurrentStateAsync(stateMachineGrain);
                metadata["currentState"] = currentState ?? "Unknown";
                metadata["lastActivity"] = DateTime.UtcNow;

                // Check for stuck states or other state-specific health indicators
                var healthStatus = AnalyzeStateMachineHealth(currentState, metadata);
                
                return (healthStatus, currentState, metadata);
            }

            return (HealthStatus.Healthy, null, metadata);
        }
        catch (Exception ex)
        {
            metadata["exception"] = ex.Message;
            return (HealthStatus.Unhealthy, null, metadata);
        }
    }

    private async Task<bool> PingGrainAsync(IGrainWithStringKey grain)
    {
        try
        {
            // Attempt to call a basic method on the grain
            // This is grain-specific and would need to be adapted per grain type
            
            if (grain is IStateMachineGrain<Enum, Enum> stateMachineGrain)
            {
                await stateMachineGrain.GetInfoAsync();
                return true;
            }

            // For generic grains, we'd need a standard health check interface
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> GetCurrentStateAsync(IStateMachineGrain<Enum, Enum> grain)
    {
        try
        {
            // This is a generic approach - in reality, you'd need to handle the specific TState type
            // For a fully generic solution, you'd need reflection or a common interface
            return "Unknown"; // Placeholder - would need grain-specific implementation
        }
        catch
        {
            return null;
        }
    }

    private HealthStatus AnalyzeStateMachineHealth(string? currentState, Dictionary<string, object> metadata)
    {
        if (string.IsNullOrEmpty(currentState))
            return HealthStatus.Degraded;

        // Implement state-specific health logic
        // For example, certain states might indicate degraded health
        if (_options.DegradedStates.Contains(currentState))
            return HealthStatus.Degraded;

        if (_options.UnhealthyStates.Contains(currentState))
            return HealthStatus.Unhealthy;

        return HealthStatus.Healthy;
    }

    private HealthStatus DetermineOverallHealth(IEnumerable<StateMachineHealthResult> results)
    {
        var resultsList = results.ToList();
        if (!resultsList.Any()) return HealthStatus.Unknown;

        var unhealthyCount = resultsList.Count(r => r.Status == HealthStatus.Unhealthy);
        var degradedCount = resultsList.Count(r => r.Status == HealthStatus.Degraded);
        var totalCount = resultsList.Count;

        var unhealthyPercentage = (double)unhealthyCount / totalCount;
        var degradedPercentage = (double)degradedCount / totalCount;

        if (unhealthyPercentage >= _options.UnhealthyThreshold)
            return HealthStatus.Unhealthy;

        if (unhealthyPercentage + degradedPercentage >= _options.DegradedThreshold)
            return HealthStatus.Degraded;

        return HealthStatus.Healthy;
    }

    private HealthStatus DetermineSystemHealth(IList<StateMachineHealthResult> recentResults)
    {
        if (!recentResults.Any()) return HealthStatus.Unknown;

        return DetermineOverallHealth(recentResults);
    }

    private async Task<SystemMetrics> CalculateSystemMetricsAsync()
    {
        try
        {
            // Get metrics from the OpenTelemetry metrics collection
            // This would integrate with the StateMachineMetrics class we created earlier
            
            return new SystemMetrics
            {
                TotalStateTransitions = StateMachineMetrics.GetTotalTransitions(),
                AverageTransitionTime = StateMachineMetrics.GetAverageTransitionTime(),
                TotalErrors = StateMachineMetrics.GetTotalErrors(),
                ErrorRate = StateMachineMetrics.GetErrorRate(),
                ActiveSagas = StateMachineMetrics.GetActiveSagaCount(),
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate system metrics");
            return new SystemMetrics();
        }
    }

    private List<SystemAlert> GenerateSystemAlerts(
        IList<StateMachineHealthResult> recentResults,
        SystemMetrics metrics)
    {
        var alerts = new List<SystemAlert>();

        // High error rate alert
        if (metrics.ErrorRate > _options.ErrorRateThreshold)
        {
            alerts.Add(new SystemAlert
            {
                Severity = AlertSeverity.Warning,
                Message = $"High error rate detected: {metrics.ErrorRate:P2}",
                Source = "SystemMetrics",
                CreatedAt = DateTime.UtcNow,
                Details = { ["errorRate"] = metrics.ErrorRate, ["threshold"] = _options.ErrorRateThreshold }
            });
        }

        // Too many unhealthy grains
        var unhealthyCount = recentResults.Count(r => r.Status == HealthStatus.Unhealthy);
        var unhealthyPercentage = recentResults.Any() ? (double)unhealthyCount / recentResults.Count : 0;
        
        if (unhealthyPercentage > _options.UnhealthyThreshold)
        {
            alerts.Add(new SystemAlert
            {
                Severity = AlertSeverity.Error,
                Message = $"High number of unhealthy grains: {unhealthyCount} ({unhealthyPercentage:P1})",
                Source = "HealthCheck",
                CreatedAt = DateTime.UtcNow,
                Details = { ["unhealthyCount"] = unhealthyCount, ["percentage"] = unhealthyPercentage }
            });
        }

        return alerts;
    }
}

/// <summary>
/// Configuration options for state machine health checks.
/// </summary>
public class StateMachineHealthCheckOptions
{
    /// <summary>
    /// Default timeout for individual grain health checks.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of concurrent health checks.
    /// </summary>
    public int MaxConcurrentChecks { get; set; } = 10;

    /// <summary>
    /// Whether to enable result caching.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Duration to cache individual health check results.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Duration to cache system health results.
    /// </summary>
    public TimeSpan SystemHealthCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Time window for considering results in system health calculation.
    /// </summary>
    public TimeSpan SystemHealthWindow { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Threshold for determining overall unhealthy status (percentage).
    /// </summary>
    public double UnhealthyThreshold { get; set; } = 0.25; // 25%

    /// <summary>
    /// Threshold for determining overall degraded status (percentage).
    /// </summary>
    public double DegradedThreshold { get; set; } = 0.10; // 10%

    /// <summary>
    /// Error rate threshold for generating alerts (percentage).
    /// </summary>
    public double ErrorRateThreshold { get; set; } = 0.05; // 5%

    /// <summary>
    /// States that should be considered degraded.
    /// </summary>
    public HashSet<string> DegradedStates { get; set; } = new() { "Degraded", "Warning", "Retrying" };

    /// <summary>
    /// States that should be considered unhealthy.
    /// </summary>
    public HashSet<string> UnhealthyStates { get; set; } = new() { "Error", "Failed", "Faulted", "TimedOut" };
}

/// <summary>
/// Extension methods for Dictionary.
/// </summary>
internal static class DictionaryExtensions
{
    public static Dictionary<string, object> With(this Dictionary<string, object> dict, string key, object value)
    {
        dict[key] = value;
        return dict;
    }
}