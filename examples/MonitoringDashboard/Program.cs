using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.StateMachineES.Monitoring;
using Orleans.StateMachineES.Tracing;
using Orleans.StateMachineES.Visualization;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Orleans.StateMachineES.Examples.MonitoringDashboard;

/// <summary>
/// Example application demonstrating comprehensive monitoring and observability
/// for Orleans.StateMachineES, including health checks, metrics, distributed tracing,
/// and visualization capabilities.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Orleans client
        ConfigureOrleansClient(builder);
        
        // Configure monitoring and observability
        ConfigureMonitoring(builder);
        
        // Configure health checks
        ConfigureHealthChecks(builder);
        
        // Configure API controllers
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        // Configure CORS for dashboard UI
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowDashboard", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        var app = builder.Build();

        // Configure middleware pipeline
        ConfigureMiddleware(app);

        // Start the application
        app.Run();
    }

    private static void ConfigureOrleansClient(WebApplicationBuilder builder)
    {
        builder.Services.AddOrleansClient(clientBuilder =>
        {
            clientBuilder
                .UseLocalhostClustering()
                .ConfigureApplicationParts(parts =>
                {
                    parts.AddApplicationPart(typeof(Program).Assembly);
                })
                .ConfigureLogging(logging => logging.AddConsole())
                .AddStateMachineMonitoring(options =>
                {
                    options.EnableHealthCheckCaching = true;
                    options.HealthCheckCacheDuration = TimeSpan.FromMinutes(2);
                    options.MaxConcurrentHealthChecks = 20;
                    options.EnableDetailedLogging = true;
                });
        });
    }

    private static void ConfigureMonitoring(WebApplicationBuilder builder)
    {
        // Configure OpenTelemetry
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: "Orleans.StateMachineES.MonitoringDashboard",
                    serviceVersion: "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(StateMachineActivitySource.SourceName)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.body.size", request.ContentLength);
                            activity.SetTag("http.request.client.address", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.body.size", response.ContentLength);
                        };
                    })
                    .AddConsoleExporter()
                    .AddJaegerExporter(); // For production, configure Jaeger endpoint
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(StateMachineMetrics.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddConsoleExporter()
                    .AddPrometheusExporter(); // Expose metrics for Prometheus scraping
            });

        // Configure logging
        builder.Services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Register monitoring services
        builder.Services.AddSingleton<IStateMachineHealthCheck, StateMachineHealthCheckService>();
        builder.Services.AddSingleton<StateMachineVisualizer>();
        builder.Services.AddSingleton<BatchVisualizationService>();
        builder.Services.AddSingleton<StateMachineWebVisualizer>();
        builder.Services.AddHostedService<MonitoringBackgroundService>();
    }

    private static void ConfigureHealthChecks(WebApplicationBuilder builder)
    {
        // Add comprehensive health checks
        builder.Services
            .AddHealthChecks()
            .AddStateMachineHealthCheck("statemachine-system", options =>
            {
                options.DefaultTimeout = TimeSpan.FromSeconds(10);
                options.EnableCaching = true;
                options.CacheDuration = TimeSpan.FromMinutes(1);
                options.MaxConcurrentChecks = 15;
            })
            .AddStateMachineGrainHealthChecks(new[]
            {
                new GrainHealthCheck("OrderProcessing", "order-001", TimeSpan.FromSeconds(5)),
                new GrainHealthCheck("OrderProcessing", "order-002", TimeSpan.FromSeconds(5)),
                new GrainHealthCheck("DocumentApproval", "doc-001", TimeSpan.FromSeconds(5)),
                new GrainHealthCheck("DocumentApproval", "doc-002", TimeSpan.FromSeconds(5))
            }, "statemachine-grains")
            .AddCheck<OrleansConnectionHealthCheck>("orleans-connection")
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<ExternalServiceHealthCheck>("external-services");

        // Add health check UI
        builder.Services.AddHealthChecksUI(setup =>
        {
            setup.SetEvaluationTimeInSeconds(30);
            setup.MaximumHistoryEntriesPerEndpoint(60);
            setup.SetMinimumSecondsBetweenFailureNotifications(10);
            setup.AddHealthCheckEndpoint("State Machine System", "/health");
            setup.AddHealthCheckEndpoint("Detailed Health", "/health/detailed");
        }).AddInMemoryStorage();
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Orleans.StateMachineES Monitoring API v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowDashboard");
        app.UseRouting();
        app.UseAuthorization();

        // Add OpenTelemetry middleware
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        // Map endpoints
        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/detailed", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    totalDuration = report.TotalDuration.TotalMilliseconds,
                    entries = report.Entries.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            status = kvp.Value.Status.ToString(),
                            duration = kvp.Value.Duration.TotalMilliseconds,
                            description = kvp.Value.Description,
                            data = kvp.Value.Data,
                            exception = kvp.Value.Exception?.Message
                        })
                });
                await context.Response.WriteAsync(json);
            }
        });

        // Health checks UI
        app.MapHealthChecksUI(setup =>
        {
            setup.UIPath = "/health-ui";
            setup.ApiPath = "/health-ui-api";
        });

        // Custom monitoring endpoints
        app.MapGet("/dashboard", async (HttpContext context, StateMachineWebVisualizer visualizer) =>
        {
            var html = await visualizer.GenerateSystemDashboardAsync();
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);
        });

        app.MapGet("/metrics/custom", async (StateMachineMetrics metrics) =>
        {
            var customMetrics = StateMachineMetrics.GetCurrentMetrics();
            return Results.Json(customMetrics);
        });

        app.MapGet("/tracing/activities", async (HttpContext context) =>
        {
            // Return information about current tracing activities
            var activities = new
            {
                source = StateMachineActivitySource.SourceName,
                version = StateMachineActivitySource.SourceVersion,
                activeSpans = System.Diagnostics.Activity.Current != null,
                currentActivity = System.Diagnostics.Activity.Current?.DisplayName
            };
            return Results.Json(activities);
        });

        // Visualization endpoints
        app.MapGet("/visualization/system", async (BatchVisualizationService visualizationService) =>
        {
            var systemVisualization = await visualizationService.GenerateSystemVisualizationAsync();
            return Results.Json(systemVisualization);
        });

        app.MapGet("/visualization/grain/{grainType}/{grainId}", async (
            string grainType, 
            string grainId, 
            StateMachineVisualizer visualizer,
            string format = "json") =>
        {
            // This would generate visualization for a specific grain
            var visualization = new
            {
                grainType = grainType,
                grainId = grainId,
                format = format,
                generatedAt = DateTime.UtcNow,
                message = "Visualization endpoint - would generate actual state machine diagram"
            };
            return Results.Json(visualization);
        });

        // Static files for dashboard UI
        app.UseStaticFiles();
        
        // Fallback for SPA routing
        app.MapFallbackToFile("index.html");
    }
}

/// <summary>
/// Background service for continuous monitoring and alerting.
/// </summary>
public class MonitoringBackgroundService : BackgroundService
{
    private readonly ILogger<MonitoringBackgroundService> _logger;
    private readonly IStateMachineHealthCheck _healthCheck;
    private readonly IServiceProvider _serviceProvider;

    public MonitoringBackgroundService(
        ILogger<MonitoringBackgroundService> logger,
        IStateMachineHealthCheck healthCheck,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _healthCheck = healthCheck;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Monitoring background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMonitoringCycle(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring cycle");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Monitoring background service stopped");
    }

    private async Task PerformMonitoringCycle(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        try
        {
            // Check system health
            var systemHealth = await _healthCheck.GetSystemHealthAsync(cancellationToken);
            
            // Log system status
            _logger.LogInformation(
                "System Health Check: {Status}, {GrainCount} grains monitored, {AlertCount} alerts",
                systemHealth.Status,
                systemHealth.TotalMonitoredGrains,
                systemHealth.Alerts.Count);

            // Log critical alerts
            foreach (var alert in systemHealth.Alerts.Where(a => a.Severity >= AlertSeverity.Error))
            {
                _logger.LogError("Critical Alert: {Message} from {Source}", alert.Message, alert.Source);
            }

            // Update metrics
            StateMachineMetrics.RecordSystemHealth(systemHealth.Status.ToString(), systemHealth.TotalMonitoredGrains);
            
            // Record health check duration
            var healthCheckDuration = TimeSpan.FromMilliseconds(100); // Would be actual duration
            StateMachineMetrics.RecordHealthCheck("system", healthCheckDuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform health check");
            StateMachineMetrics.RecordHealthCheck("system_error", TimeSpan.Zero);
        }
    }
}

    /// <summary>
    /// Health check for Orleans cluster connection.
    /// </summary>
    public class OrleansConnectionHealthCheck : IHealthCheck
    {
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<OrleansConnectionHealthCheck> _logger;

        public OrleansConnectionHealthCheck(IClusterClient clusterClient, ILogger<OrleansConnectionHealthCheck> logger)
        {
            _clusterClient = clusterClient;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Test Orleans connection by getting a simple grain
                var testGrain = _clusterClient.GetGrain<IGrainWithStringKey>("health-check");
                await Task.Delay(10, cancellationToken); // Simple connectivity test

                return HealthCheckResult.Healthy("Orleans cluster connection is active");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orleans connection health check failed");
                return HealthCheckResult.Unhealthy("Orleans cluster connection failed", ex);
            }
        }
    }

    /// <summary>
    /// Health check for database connectivity.
    /// </summary>
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(ILogger<DatabaseHealthCheck> logger)
        {
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Simulate database connectivity check
                await Task.Delay(50, cancellationToken);
                
                // In a real implementation, this would test actual database connection
                var isHealthy = true; // Would be actual database check result
                
                if (isHealthy)
                {
                    return HealthCheckResult.Healthy("Database connection is healthy");
                }
                else
                {
                    return HealthCheckResult.Degraded("Database connection is slow");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy("Database connection failed", ex);
            }
        }
    }

    /// <summary>
    /// Health check for external service dependencies.
    /// </summary>
    public class ExternalServiceHealthCheck : IHealthCheck
    {
        private readonly ILogger<ExternalServiceHealthCheck> _logger;
        private readonly HttpClient _httpClient;

        public ExternalServiceHealthCheck(ILogger<ExternalServiceHealthCheck> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("HealthCheck");
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check external services (payment gateway, inventory service, etc.)
                var services = new[]
                {
                    ("PaymentService", "https://api.payment.example.com/health"),
                    ("InventoryService", "https://api.inventory.example.com/health"),
                    ("NotificationService", "https://api.notifications.example.com/health")
                };

                var results = new Dictionary<string, bool>();
                
                foreach (var (serviceName, healthUrl) in services)
                {
                    try
                    {
                        // In a real implementation, make actual HTTP calls
                        await Task.Delay(25, cancellationToken); // Simulate network call
                        results[serviceName] = true; // Would be actual HTTP response check
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "External service {ServiceName} health check failed", serviceName);
                        results[serviceName] = false;
                    }
                }

                var healthyServices = results.Values.Count(v => v);
                var totalServices = results.Count;

                if (healthyServices == totalServices)
                {
                    return HealthCheckResult.Healthy($"All {totalServices} external services are healthy", results);
                }
                else if (healthyServices > totalServices / 2)
                {
                    return HealthCheckResult.Degraded($"{healthyServices}/{totalServices} external services are healthy", results);
                }
                else
                {
                    return HealthCheckResult.Unhealthy($"Only {healthyServices}/{totalServices} external services are healthy", data: results);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External services health check failed");
                return HealthCheckResult.Unhealthy("External services health check failed", ex);
            }
        }
    }