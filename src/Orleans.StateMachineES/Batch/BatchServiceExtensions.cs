using Microsoft.Extensions.DependencyInjection;

namespace Orleans.StateMachineES.Batch;

/// <summary>
/// Extension methods for registering batch services with dependency injection.
/// </summary>
public static class BatchServiceExtensions
{
    /// <summary>
    /// Adds batch state machine services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBatchStateMachineService(this IServiceCollection services)
    {
        services.AddSingleton<IBatchStateMachineService, BatchStateMachineService>();
        return services;
    }

    /// <summary>
    /// Adds batch state machine services to the Orleans silo builder.
    /// </summary>
    /// <param name="builder">The silo builder.</param>
    /// <returns>The silo builder for chaining.</returns>
    public static ISiloBuilder AddBatchStateMachineService(this ISiloBuilder builder)
    {
        builder.ConfigureServices(services => services.AddBatchStateMachineService());
        return builder;
    }
}
