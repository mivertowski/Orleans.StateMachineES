using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Orleans.StateMachineES.Tracing;

/// <summary>
/// Extension methods for configuring OpenTelemetry tracing in Orleans.StateMachineES applications.
/// Provides easy setup for distributed tracing with proper resource detection and instrumentation.
/// </summary>
public static class TracingExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing support for Orleans.StateMachineES to the host builder.
    /// Configures automatic instrumentation for state machines, sagas, and Orleans operations.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <param name="configure">Optional configuration delegate for customizing tracing.</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder AddStateMachineTracing(
        this IHostBuilder hostBuilder,
        Action<TracingConfiguration>? configure = null)
    {
        var config = new TracingConfiguration();
        configure?.Invoke(config);
        
        return hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resourceBuilder =>
                {
                    resourceBuilder
                        .AddService(
                            serviceName: config.ServiceName ?? context.HostingEnvironment.ApplicationName ?? "Orleans.StateMachineES",
                            serviceVersion: config.ServiceVersion ?? "1.0.0",
                            serviceInstanceId: config.ServiceInstanceId ?? Environment.MachineName)
                        .AddAttributes(config.ResourceAttributes);
                })
                .WithTracing(tracingBuilder =>
                {
                    tracingBuilder
                        .AddSource(StateMachineActivitySource.SourceName)
                        .SetSampler(config.Sampler ?? new AlwaysOnSampler());
                    
                    // Orleans instrumentation requires Orleans.TelemetryConsumers package.
                    // Currently relying on state machine-specific tracing via StateMachineActivitySource.
                    // Add the package reference and .AddOrleans() call here for Orleans runtime metrics.
                    
                    // Add ASP.NET Core instrumentation if configured
                    if (config.IncludeAspNetCoreInstrumentation)
                    {
                        tracingBuilder.AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = config.RecordExceptions;
                            options.EnrichWithHttpRequest = config.EnrichWithHttpRequest;
                            options.EnrichWithHttpResponse = config.EnrichWithHttpResponse;
                        });
                    }
                    
                    // Add HTTP client instrumentation if configured
                    if (config.IncludeHttpClientInstrumentation)
                    {
                        tracingBuilder.AddHttpClientInstrumentation(options =>
                        {
                            options.RecordException = config.RecordExceptions;
                            options.EnrichWithHttpRequestMessage = config.EnrichWithHttpRequest;
                            options.EnrichWithHttpResponseMessage = config.EnrichWithHttpResponse;
                        });
                    }
                    
                    // SQL client instrumentation commented out due to package version issues
                    // Add SQL client instrumentation if configured
                    // if (config.IncludeSqlClientInstrumentation)
                    // {
                    //     tracingBuilder.AddSqlClientInstrumentation(options =>
                    //     {
                    //         options.RecordException = config.RecordExceptions;
                    //         options.SetDbStatementForText = config.RecordSqlStatements;
                    //         options.SetDbStatementForStoredProcedure = config.RecordSqlStatements;
                    //     });
                    // }
                    
                    // Configure exporters
                    foreach (var exporterConfig in config.Exporters)
                    {
                        exporterConfig(tracingBuilder);
                    }
                    
                    // Apply custom configuration
                    config.CustomConfiguration?.Invoke(tracingBuilder);
                });
            
            // Add metrics if enabled
            if (config.IncludeMetrics)
            {
                services.AddOpenTelemetry()
                    .WithMetrics(metricsBuilder =>
                    {
                        metricsBuilder
                            .AddMeter(StateMachineMetrics.MeterName);
                        
                        // Configure metrics exporters
                        foreach (var exporterConfig in config.MetricsExporters)
                        {
                            exporterConfig(metricsBuilder);
                        }
                        
                        // Apply custom metrics configuration
                        config.CustomMetricsConfiguration?.Invoke(metricsBuilder);
                    });
            }
        });
    }
    
    /// <summary>
    /// Adds OpenTelemetry tracing support for Orleans.StateMachineES to the silo builder.
    /// Configures automatic instrumentation for Orleans-specific operations.
    /// </summary>
    /// <param name="siloBuilder">The silo builder to configure.</param>
    /// <param name="configure">Optional configuration delegate for customizing tracing.</param>
    /// <returns>The silo builder for chaining.</returns>
    public static ISiloBuilder AddStateMachineTracing(
        this ISiloBuilder siloBuilder,
        Action<TracingConfiguration>? configure = null)
    {
        var config = new TracingConfiguration();
        configure?.Invoke(config);
        
        return siloBuilder.ConfigureServices(services =>
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resourceBuilder =>
                {
                    resourceBuilder
                        .AddService(
                            serviceName: config.ServiceName ?? "Orleans.StateMachineES.Silo",
                            serviceVersion: config.ServiceVersion ?? "1.0.0",
                            serviceInstanceId: config.ServiceInstanceId ?? Environment.MachineName)
                        .AddAttributes(config.ResourceAttributes);
                })
                .WithTracing(tracingBuilder =>
                {
                    tracingBuilder
                        .AddSource(StateMachineActivitySource.SourceName)
                        .SetSampler(config.Sampler ?? new AlwaysOnSampler());
                    
                    // Configure exporters for silo
                    foreach (var exporterConfig in config.Exporters)
                    {
                        exporterConfig(tracingBuilder);
                    }
                    
                    // Apply custom configuration
                    config.CustomConfiguration?.Invoke(tracingBuilder);
                });
        });
    }
    
    /// <summary>
    /// Adds console exporter for development and debugging purposes.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration AddConsoleExporter(this TracingConfiguration config)
    {
        config.Exporters.Add(builder => builder.AddConsoleExporter());
        return config;
    }
    
    /// <summary>
    /// Adds OTLP (OpenTelemetry Protocol) exporter for production telemetry systems.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <param name="endpoint">The OTLP endpoint URL.</param>
    /// <param name="configure">Optional OTLP configuration delegate.</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration AddOtlpExporter(
        this TracingConfiguration config,
        string? endpoint = null,
        Action<OpenTelemetry.Exporter.OtlpExporterOptions>? configure = null)
    {
        config.Exporters.Add(builder => builder.AddOtlpExporter(options =>
        {
            if (!string.IsNullOrEmpty(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }
            configure?.Invoke(options);
        }));
        return config;
    }
    
    /// <summary>
    /// Adds Jaeger exporter for distributed tracing visualization.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <param name="configure">Optional Jaeger configuration delegate.</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration AddJaegerExporter(
        this TracingConfiguration config,
        Action<OpenTelemetry.Exporter.JaegerExporterOptions>? configure = null)
    {
        config.Exporters.Add(builder => builder.AddJaegerExporter(configure));
        return config;
    }
    
    /// <summary>
    /// Adds Zipkin exporter for distributed tracing.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <param name="endpoint">The Zipkin endpoint URL.</param>
    /// <param name="configure">Optional Zipkin configuration delegate.</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration AddZipkinExporter(
        this TracingConfiguration config,
        string? endpoint = null,
        Action<OpenTelemetry.Exporter.ZipkinExporterOptions>? configure = null)
    {
        config.Exporters.Add(builder => builder.AddZipkinExporter(options =>
        {
            if (!string.IsNullOrEmpty(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }
            configure?.Invoke(options);
        }));
        return config;
    }
    
    /// <summary>
    /// Configures sampling for high-throughput scenarios.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <param name="ratio">The sampling ratio (0.0 to 1.0).</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration WithSampling(this TracingConfiguration config, double ratio)
    {
        config.Sampler = new TraceIdRatioBasedSampler(ratio);
        return config;
    }
    
    /// <summary>
    /// Enables comprehensive instrumentation for web applications.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <param name="recordExceptions">Whether to record exception details.</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration WithWebInstrumentation(this TracingConfiguration config, bool recordExceptions = true)
    {
        config.IncludeAspNetCoreInstrumentation = true;
        config.IncludeHttpClientInstrumentation = true;
        config.RecordExceptions = recordExceptions;
        return config;
    }
    
    /// <summary>
    /// Enables database instrumentation for SQL operations.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <param name="recordStatements">Whether to record SQL statements (be careful with sensitive data).</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration WithDatabaseInstrumentation(this TracingConfiguration config, bool recordStatements = false)
    {
        config.IncludeSqlClientInstrumentation = true;
        config.RecordSqlStatements = recordStatements;
        return config;
    }
    
    /// <summary>
    /// Adds console metrics exporter for development and debugging purposes.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration AddConsoleMetricsExporter(this TracingConfiguration config)
    {
        config.MetricsExporters.Add(builder => builder.AddConsoleExporter());
        return config;
    }
    
    /// <summary>
    /// Adds OTLP (OpenTelemetry Protocol) metrics exporter for production telemetry systems.
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <param name="endpoint">The OTLP endpoint URL.</param>
    /// <param name="configure">Optional OTLP configuration delegate.</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration AddOtlpMetricsExporter(
        this TracingConfiguration config,
        string? endpoint = null,
        Action<OpenTelemetry.Exporter.OtlpExporterOptions>? configure = null)
    {
        config.MetricsExporters.Add(builder => builder.AddOtlpExporter(options =>
        {
            if (!string.IsNullOrEmpty(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }
            configure?.Invoke(options);
        }));
        return config;
    }
    
    /// <summary>
    /// Disables metrics collection (tracing only).
    /// </summary>
    /// <param name="config">The tracing configuration to modify.</param>
    /// <returns>The configuration for chaining.</returns>
    public static TracingConfiguration WithoutMetrics(this TracingConfiguration config)
    {
        config.IncludeMetrics = false;
        return config;
    }
}

/// <summary>
/// Configuration options for OpenTelemetry tracing in Orleans.StateMachineES.
/// Provides fine-grained control over telemetry collection and export.
/// </summary>
public class TracingConfiguration
{
    /// <summary>
    /// The service name to use for telemetry identification.
    /// </summary>
    public string? ServiceName { get; set; }
    
    /// <summary>
    /// The service version for telemetry.
    /// </summary>
    public string? ServiceVersion { get; set; }
    
    /// <summary>
    /// The service instance identifier.
    /// </summary>
    public string? ServiceInstanceId { get; set; }
    
    /// <summary>
    /// Additional resource attributes for telemetry.
    /// </summary>
    public Dictionary<string, object> ResourceAttributes { get; set; } = [];
    
    /// <summary>
    /// The sampling strategy to use for trace collection.
    /// </summary>
    public Sampler? Sampler { get; set; }
    
    /// <summary>
    /// Whether to include Orleans-specific instrumentation.
    /// </summary>
    public bool IncludeOrleansInstrumentation { get; set; } = true;
    
    /// <summary>
    /// Whether to include ASP.NET Core instrumentation.
    /// </summary>
    public bool IncludeAspNetCoreInstrumentation { get; set; }
    
    /// <summary>
    /// Whether to include HTTP client instrumentation.
    /// </summary>
    public bool IncludeHttpClientInstrumentation { get; set; }
    
    /// <summary>
    /// Whether to include SQL client instrumentation.
    /// </summary>
    public bool IncludeSqlClientInstrumentation { get; set; }
    
    /// <summary>
    /// Whether to record exception details in traces.
    /// </summary>
    public bool RecordExceptions { get; set; } = true;
    
    /// <summary>
    /// Whether to record SQL statements (use carefully due to potential sensitive data).
    /// </summary>
    public bool RecordSqlStatements { get; set; }
    
    /// <summary>
    /// Custom HTTP request enrichment function.
    /// </summary>
    public Action<Activity, object>? EnrichWithHttpRequest { get; set; }
    
    /// <summary>
    /// Custom HTTP response enrichment function.
    /// </summary>
    public Action<Activity, object>? EnrichWithHttpResponse { get; set; }
    
    /// <summary>
    /// List of exporter configuration delegates.
    /// </summary>
    public List<Action<TracerProviderBuilder>> Exporters { get; set; } = [];
    
    /// <summary>
    /// Custom tracing configuration delegate for advanced scenarios.
    /// </summary>
    public Action<TracerProviderBuilder>? CustomConfiguration { get; set; }
    
    /// <summary>
    /// Whether to include metrics collection alongside tracing.
    /// </summary>
    public bool IncludeMetrics { get; set; } = true;
    
    /// <summary>
    /// List of metrics exporter configuration delegates.
    /// </summary>
    public List<Action<MeterProviderBuilder>> MetricsExporters { get; set; } = [];
    
    /// <summary>
    /// Custom metrics configuration delegate for advanced scenarios.
    /// </summary>
    public Action<MeterProviderBuilder>? CustomMetricsConfiguration { get; set; }
}