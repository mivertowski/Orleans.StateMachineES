using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orleans.StateMachineES.Examples.MonitoringDashboard;

/// <summary>
/// Simple monitoring dashboard example demonstrating basic health checks.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        // Add basic health checks
        builder.Services.AddHealthChecks()
            .AddCheck<SimpleHealthCheck>("application");

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        // Map health check endpoints
        app.MapHealthChecks("/health");
        
        // Simple dashboard endpoint
        app.MapGet("/dashboard", () => new
        {
            message = "Orleans.StateMachineES Monitoring Dashboard",
            timestamp = DateTime.UtcNow,
            features = new[]
            {
                "Health Checks",
                "State Machine Monitoring",
                "Distributed Tracing",
                "Metrics Collection",
                "Visualization"
            }
        });

        app.Run();
    }
}

/// <summary>
/// Simple health check implementation.
/// </summary>
public class SimpleHealthCheck : IHealthCheck
{
    private readonly ILogger<SimpleHealthCheck> _logger;

    public SimpleHealthCheck(ILogger<SimpleHealthCheck> logger)
    {
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simulate health check
            await Task.Delay(10, cancellationToken);
            
            _logger.LogInformation("Health check executed successfully");
            
            return HealthCheckResult.Healthy("Application is healthy", new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["status"] = "OK"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy("Application is unhealthy", ex);
        }
    }
}