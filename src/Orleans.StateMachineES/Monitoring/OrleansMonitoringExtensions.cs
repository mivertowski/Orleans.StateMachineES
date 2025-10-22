using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orleans.StateMachineES.Monitoring;

/// <summary>
/// Extensions for configuring state machine monitoring in Orleans silos and clients.
/// </summary>
public static class OrleansMonitoringExtensions
{
    /// <summary>
    /// Adds state machine monitoring services to an Orleans silo.
    /// </summary>
    /// <param name="siloBuilder">The silo builder.</param>
    /// <param name="configureOptions">Action to configure monitoring options.</param>
    /// <returns>The silo builder for chaining.</returns>
    public static ISiloBuilder AddStateMachineMonitoring(
        this ISiloBuilder siloBuilder,
        Action<StateMachineMonitoringOptions>? configureOptions = null)
    {
        return siloBuilder.ConfigureServices(services =>
        {
            // Configure monitoring options
            var options = new StateMachineMonitoringOptions();
            configureOptions?.Invoke(options);
            services.AddSingleton(options);

            // Add monitoring services
            services.AddSingleton<IStateMachineHealthCheck, StateMachineHealthCheckService>();
            services.AddSingleton(provider => new StateMachineHealthCheckOptions
            {
                DefaultTimeout = options.HealthCheckTimeout,
                EnableCaching = options.EnableHealthCheckCaching,
                CacheDuration = options.HealthCheckCacheDuration,
                MaxConcurrentChecks = options.MaxConcurrentHealthChecks,
                UnhealthyThreshold = options.UnhealthyThreshold,
                DegradedThreshold = options.DegradedThreshold,
                ErrorRateThreshold = options.ErrorRateThreshold
            });

            // Add background monitoring service if enabled
            if (options.EnableBackgroundMonitoring)
            {
                services.AddSingleton<IHostedService, StateMachineMonitoringBackgroundService>();
            }

            // Add monitoring interceptors if enabled
            if (options.EnableInterception)
            {
                // This would add interceptors for automatic monitoring
                // Implementation would depend on Orleans interceptor framework
            }
        });
    }

    /// <summary>
    /// Adds state machine monitoring services to an Orleans client.
    /// </summary>
    /// <param name="clientBuilder">The client builder.</param>
    /// <param name="configureOptions">Action to configure monitoring options.</param>
    /// <returns>The client builder for chaining.</returns>
    public static IClientBuilder AddStateMachineMonitoring(
        this IClientBuilder clientBuilder,
        Action<StateMachineMonitoringOptions>? configureOptions = null)
    {
        return clientBuilder.ConfigureServices(services =>
        {
            // Configure monitoring options
            var options = new StateMachineMonitoringOptions();
            configureOptions?.Invoke(options);
            services.AddSingleton(options);

            // Add client-side monitoring services
            services.AddSingleton<IStateMachineHealthCheck, StateMachineHealthCheckService>();
            services.AddSingleton(provider => new StateMachineHealthCheckOptions
            {
                DefaultTimeout = options.HealthCheckTimeout,
                EnableCaching = options.EnableHealthCheckCaching,
                CacheDuration = options.HealthCheckCacheDuration,
                MaxConcurrentChecks = options.MaxConcurrentHealthChecks
            });
        });
    }
}

/// <summary>
/// Configuration options for state machine monitoring.
/// </summary>
public class StateMachineMonitoringOptions
{
    /// <summary>
    /// Default timeout for health checks.
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether to enable health check result caching.
    /// </summary>
    public bool EnableHealthCheckCaching { get; set; } = true;

    /// <summary>
    /// Duration to cache health check results.
    /// </summary>
    public TimeSpan HealthCheckCacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum number of concurrent health checks.
    /// </summary>
    public int MaxConcurrentHealthChecks { get; set; } = 10;

    /// <summary>
    /// Threshold for unhealthy status (percentage of unhealthy grains).
    /// </summary>
    public double UnhealthyThreshold { get; set; } = 0.25; // 25%

    /// <summary>
    /// Threshold for degraded status (percentage of degraded grains).
    /// </summary>
    public double DegradedThreshold { get; set; } = 0.10; // 10%

    /// <summary>
    /// Error rate threshold for alerts.
    /// </summary>
    public double ErrorRateThreshold { get; set; } = 0.05; // 5%

    /// <summary>
    /// Whether to enable background monitoring service.
    /// </summary>
    public bool EnableBackgroundMonitoring { get; set; } = false;

    /// <summary>
    /// Interval for background monitoring checks.
    /// </summary>
    public TimeSpan BackgroundMonitoringInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to enable automatic monitoring through interceptors.
    /// </summary>
    public bool EnableInterception { get; set; } = true;

    /// <summary>
    /// List of grain types to monitor automatically.
    /// If empty, all state machine grains are monitored.
    /// </summary>
    public string[] MonitoredGrainTypes { get; set; } = [];

    /// <summary>
    /// Whether to expose monitoring endpoints via HTTP.
    /// </summary>
    public bool ExposeHttpEndpoints { get; set; } = true;

    /// <summary>
    /// Base path for monitoring HTTP endpoints.
    /// </summary>
    public string HttpEndpointBasePath { get; set; } = "/api/statemachine/monitoring";

    /// <summary>
    /// Whether to integrate with ASP.NET Core health checks.
    /// </summary>
    public bool EnableAspNetCoreHealthChecks { get; set; } = true;

    /// <summary>
    /// Whether to enable detailed logging for monitoring operations.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Whether to publish monitoring events to Orleans streams.
    /// </summary>
    public bool PublishToStreams { get; set; } = false;

    /// <summary>
    /// Stream provider name for publishing monitoring events.
    /// </summary>
    public string? MonitoringStreamProvider { get; set; }

    /// <summary>
    /// Namespace for monitoring streams.
    /// </summary>
    public string MonitoringStreamNamespace { get; set; } = "Orleans.StateMachineES.Monitoring";
}

/// <summary>
/// Background service for continuous state machine monitoring.
/// </summary>
/// <remarks>
/// Initializes a new instance of the background monitoring service.
/// </remarks>
/// <param name="healthCheck">Health check service.</param>
/// <param name="options">Monitoring options.</param>
/// <param name="logger">Logger instance.</param>
public class StateMachineMonitoringBackgroundService(
    IStateMachineHealthCheck healthCheck,
    StateMachineMonitoringOptions options,
    ILogger<StateMachineMonitoringBackgroundService> logger) : BackgroundService
{
    private readonly IStateMachineHealthCheck _healthCheck = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
    private readonly StateMachineMonitoringOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<StateMachineMonitoringBackgroundService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("State machine monitoring background service started with interval {Interval}",
            _options.BackgroundMonitoringInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMonitoringCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during monitoring cycle");
            }

            try
            {
                await Task.Delay(_options.BackgroundMonitoringInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("State machine monitoring background service stopped");
    }

    private async Task PerformMonitoringCycleAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        _logger.LogDebug("Starting monitoring cycle at {StartTime}", startTime);

        try
        {
            // Get system health
            var systemHealth = await _healthCheck.GetSystemHealthAsync(cancellationToken);

            // Log system health summary
            _logger.LogInformation(
                "Monitoring cycle completed: {Status}, {TotalGrains} grains monitored, {AlertCount} alerts",
                systemHealth.Status,
                systemHealth.TotalMonitoredGrains,
                systemHealth.Alerts.Count);

            // Log individual alerts
            foreach (var alert in systemHealth.Alerts.Where(a => a.Severity >= AlertSeverity.Warning))
            {
                _logger.LogWarning("System Alert [{Severity}]: {Message} from {Source}",
                    alert.Severity, alert.Message, alert.Source);
            }

            // Publish to streams if enabled
            if (_options.PublishToStreams && !string.IsNullOrEmpty(_options.MonitoringStreamProvider))
            {
                await PublishMonitoringEventAsync(systemHealth, cancellationToken);
            }
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Monitoring cycle completed in {Duration}ms", duration.TotalMilliseconds);
        }
    }

    private async Task PublishMonitoringEventAsync(SystemHealthResult healthResult, CancellationToken cancellationToken)
    {
        try
        {
            // Implementation would depend on Orleans streams configuration
            // This is a placeholder for stream publishing logic
            await Task.CompletedTask;

            _logger.LogDebug("Published monitoring event to stream");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish monitoring event to stream");
        }
    }
}