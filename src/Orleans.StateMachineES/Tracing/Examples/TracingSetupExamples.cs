using Microsoft.Extensions.Hosting;

namespace Orleans.StateMachineES.Tracing.Examples;

/// <summary>
/// Example configurations for setting up distributed tracing and metrics in Orleans.StateMachineES applications.
/// These examples show various scenarios from development to production environments.
/// </summary>
public static class TracingSetupExamples
{
    /// <summary>
    /// Basic setup for development environment with console output.
    /// Includes both tracing and metrics with console exporters for easy debugging.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <returns>The configured host builder.</returns>
    public static IHostBuilder AddBasicDevelopmentTracing(this IHostBuilder hostBuilder)
    {
        return hostBuilder.AddStateMachineTracing(config =>
        {
            config.ServiceName = "StateMachine-Development";
            config.ServiceVersion = "dev";
            
            // Add console exporters for both traces and metrics
            config.AddConsoleExporter()
                  .AddConsoleMetricsExporter();
            
            // Enable comprehensive instrumentation for development
            config.WithWebInstrumentation(recordExceptions: true)
                  .WithDatabaseInstrumentation(recordStatements: true);
            
            // Use full sampling during development
            config.WithSampling(1.0);
        });
    }
    
    /// <summary>
    /// Production setup with OTLP exporters for observability platforms like Grafana, Datadog, etc.
    /// Includes proper sampling and resource attributes for production monitoring.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <param name="otlpEndpoint">The OTLP collector endpoint URL.</param>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="serviceVersion">The version of the service.</param>
    /// <returns>The configured host builder.</returns>
    public static IHostBuilder AddProductionTracing(
        this IHostBuilder hostBuilder,
        string otlpEndpoint,
        string serviceName,
        string serviceVersion)
    {
        return hostBuilder.AddStateMachineTracing(config =>
        {
            config.ServiceName = serviceName;
            config.ServiceVersion = serviceVersion;
            config.ServiceInstanceId = Environment.MachineName;
            
            // Add resource attributes for better filtering in observability platforms
            config.ResourceAttributes.Add("deployment.environment", "production");
            config.ResourceAttributes.Add("service.namespace", "statemachine");
            
            // Configure OTLP exporters for both traces and metrics
            config.AddOtlpExporter(otlpEndpoint)
                  .AddOtlpMetricsExporter(otlpEndpoint);
            
            // Enable web instrumentation but be careful with sensitive data
            config.WithWebInstrumentation(recordExceptions: true)
                  .WithDatabaseInstrumentation(recordStatements: false);
            
            // Use reasonable sampling for production (10% of traces)
            config.WithSampling(0.1);
        });
    }
    
    /// <summary>
    /// Setup for Jaeger distributed tracing with local development.
    /// Useful when running Jaeger locally or in development Kubernetes clusters.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <returns>The configured host builder.</returns>
    public static IHostBuilder AddJaegerTracing(this IHostBuilder hostBuilder)
    {
        return hostBuilder.AddStateMachineTracing(config =>
        {
            config.ServiceName = "StateMachine-Local";
            config.AddJaegerExporter(options =>
            {
                options.AgentHost = "localhost";
                options.AgentPort = 6831;
            });
            
            // Add console metrics since Jaeger focuses on traces
            config.AddConsoleMetricsExporter();
            
            config.WithSampling(0.5); // 50% sampling for local development
        });
    }
    
    /// <summary>
    /// High-performance setup for scenarios where observability overhead must be minimized.
    /// Uses aggressive sampling and minimal instrumentation.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <param name="otlpEndpoint">The OTLP collector endpoint URL.</param>
    /// <returns>The configured host builder.</returns>
    public static IHostBuilder AddMinimalTracing(
        this IHostBuilder hostBuilder,
        string otlpEndpoint)
    {
        return hostBuilder.AddStateMachineTracing(config =>
        {
            config.ServiceName = "StateMachine-HighPerf";
            config.AddOtlpExporter(otlpEndpoint);
            
            // Minimal instrumentation - only state machine operations
            config.IncludeOrleansInstrumentation = false;
            config.IncludeAspNetCoreInstrumentation = false;
            config.IncludeHttpClientInstrumentation = false;
            config.IncludeSqlClientInstrumentation = false;
            
            // Very aggressive sampling (1% of traces)
            config.WithSampling(0.01);
            
            // Disable metrics to reduce overhead further
            config.WithoutMetrics();
        });
    }
    
    /// <summary>
    /// Setup for Orleans silo with comprehensive observability.
    /// Includes all available instrumentation for complete visibility.
    /// </summary>
    /// <param name="siloBuilder">The silo builder to configure.</param>
    /// <returns>The configured silo builder.</returns>
    public static ISiloBuilder AddComprehensiveSiloTracing(this ISiloBuilder siloBuilder)
    {
        return siloBuilder.AddStateMachineTracing(config =>
        {
            config.ServiceName = "StateMachine-Silo";
            config.ServiceVersion = "1.0.0";
            
            // Add multiple exporters for redundancy
            config.AddOtlpExporter()
                  .AddConsoleExporter();
            
            // Add metrics exporters
            config.AddOtlpMetricsExporter()
                  .AddConsoleMetricsExporter();
            
            // Full Orleans instrumentation
            config.IncludeOrleansInstrumentation = true;
            config.RecordExceptions = true;
            
            // Balanced sampling for production silo
            config.WithSampling(0.2);
            
            // Add custom resource attributes for silo identification
            config.ResourceAttributes.Add("orleans.silo.type", "primary");
            config.ResourceAttributes.Add("orleans.cluster.id", Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID") ?? "default");
        });
    }
    
    /// <summary>
    /// Custom setup example showing advanced configuration with custom enrichment.
    /// Demonstrates how to add business-specific context to traces and metrics.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <returns>The configured host builder.</returns>
    public static IHostBuilder AddCustomEnrichedTracing(this IHostBuilder hostBuilder)
    {
        return hostBuilder.AddStateMachineTracing(config =>
        {
            config.ServiceName = "StateMachine-Enriched";
            config.AddOtlpExporter();
            
            // Custom HTTP request enrichment
            config.EnrichWithHttpRequest = (activity, request) =>
            {
                // Add business-specific context from HTTP headers
                if (request is Microsoft.AspNetCore.Http.HttpRequest httpRequest)
                {
                    if (httpRequest.Headers.TryGetValue("X-Business-Unit", out var businessUnit))
                    {
                        activity.SetTag("business.unit", businessUnit.ToString());
                    }
                    
                    if (httpRequest.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
                    {
                        activity.SetTag("tenant.id", tenantId.ToString());
                    }
                }
            };
            
            // Custom tracing configuration for additional sources
            config.CustomConfiguration = builder =>
            {
                builder.AddSource("MyCompany.BusinessLogic")
                       .AddSource("MyCompany.Integration");
            };
            
            // Custom metrics configuration
            config.CustomMetricsConfiguration = builder =>
            {
                builder.AddMeter("MyCompany.Business.Metrics");
            };
            
            config.WithWebInstrumentation(recordExceptions: true);
        });
    }
    
    /// <summary>
    /// Example showing how to configure different tracing for different environments.
    /// Uses environment variables to determine configuration.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <returns>The configured host builder.</returns>
    public static IHostBuilder AddEnvironmentAwareTracing(this IHostBuilder hostBuilder)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT");
        var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "StateMachine";
        
        return hostBuilder.AddStateMachineTracing(config =>
        {
            config.ServiceName = serviceName;
            config.ServiceVersion = typeof(TracingSetupExamples).Assembly.GetName().Version?.ToString() ?? "1.0.0";
            config.ResourceAttributes.Add("deployment.environment", environment);
            
            switch (environment.ToLower())
            {
                case "development":
                    // Development: Console output, full sampling, detailed instrumentation
                    config.AddConsoleExporter()
                          .AddConsoleMetricsExporter()
                          .WithWebInstrumentation(recordExceptions: true)
                          .WithDatabaseInstrumentation(recordStatements: true)
                          .WithSampling(1.0);
                    break;
                    
                case "staging":
                    // Staging: OTLP + Console, medium sampling, full instrumentation
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        config.AddOtlpExporter(otlpEndpoint)
                              .AddOtlpMetricsExporter(otlpEndpoint);
                    }
                    config.AddConsoleExporter()
                          .WithWebInstrumentation(recordExceptions: true)
                          .WithDatabaseInstrumentation(recordStatements: false)
                          .WithSampling(0.5);
                    break;
                    
                case "production":
                    // Production: OTLP only, conservative sampling, secure instrumentation
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        config.AddOtlpExporter(otlpEndpoint)
                              .AddOtlpMetricsExporter(otlpEndpoint);
                    }
                    else
                    {
                        throw new InvalidOperationException("OTLP_ENDPOINT environment variable is required for production");
                    }
                    
                    config.WithWebInstrumentation(recordExceptions: false)
                          .WithDatabaseInstrumentation(recordStatements: false)
                          .WithSampling(0.1);
                    break;
                    
                default:
                    // Unknown environment: Minimal safe configuration
                    config.AddConsoleExporter()
                          .WithSampling(0.1)
                          .WithoutMetrics();
                    break;
            }
        });
    }
}