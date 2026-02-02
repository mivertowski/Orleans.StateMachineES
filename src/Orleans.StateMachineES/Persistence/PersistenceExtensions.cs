using Microsoft.Extensions.DependencyInjection;

namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// Extension methods for configuring persistence services.
/// </summary>
public static class PersistenceExtensions
{
    /// <summary>
    /// Adds state machine persistence services with default in-memory providers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateMachinePersistence(
        this IServiceCollection services,
        Action<PersistenceOptions>? configure = null)
    {
        var options = new PersistenceOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        return services;
    }

    /// <summary>
    /// Adds in-memory event store for development and testing.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryEventStore<TState, TTrigger>(
        this IServiceCollection services)
        where TState : notnull
        where TTrigger : notnull
    {
        services.AddSingleton<InMemoryEventStore<TState, TTrigger>>();
        services.AddSingleton<IEventStore<TState, TTrigger>>(
            sp => sp.GetRequiredService<InMemoryEventStore<TState, TTrigger>>());
        services.AddSingleton<IEventStore>(
            sp => sp.GetRequiredService<InMemoryEventStore<TState, TTrigger>>());

        return services;
    }

    /// <summary>
    /// Adds in-memory snapshot store for development and testing.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemorySnapshotStore<TState>(
        this IServiceCollection services)
        where TState : notnull
    {
        services.AddSingleton<InMemorySnapshotStore<TState>>();
        services.AddSingleton<ISnapshotStore<TState>>(
            sp => sp.GetRequiredService<InMemorySnapshotStore<TState>>());
        services.AddSingleton<ISnapshotStore>(
            sp => sp.GetRequiredService<InMemorySnapshotStore<TState>>());

        return services;
    }

    /// <summary>
    /// Adds in-memory combined persistence for development and testing.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryStateMachinePersistence<TState, TTrigger>(
        this IServiceCollection services,
        Action<PersistenceOptions>? configure = null)
        where TState : notnull
        where TTrigger : notnull
    {
        var options = new PersistenceOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<InMemoryStateMachinePersistence<TState, TTrigger>>();
        services.AddSingleton<IStateMachinePersistence<TState, TTrigger>>(
            sp => sp.GetRequiredService<InMemoryStateMachinePersistence<TState, TTrigger>>());
        services.AddSingleton<IEventStore<TState, TTrigger>>(
            sp => sp.GetRequiredService<InMemoryStateMachinePersistence<TState, TTrigger>>());
        services.AddSingleton<ISnapshotStore<TState>>(
            sp => sp.GetRequiredService<InMemoryStateMachinePersistence<TState, TTrigger>>());
        services.AddSingleton<IEventStore>(
            sp => sp.GetRequiredService<InMemoryStateMachinePersistence<TState, TTrigger>>());
        services.AddSingleton<ISnapshotStore>(
            sp => sp.GetRequiredService<InMemoryStateMachinePersistence<TState, TTrigger>>());

        return services;
    }

    /// <summary>
    /// Creates a persistence provider for a specific state machine type.
    /// </summary>
    /// <typeparam name="TState">The type representing the states.</typeparam>
    /// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
    /// <param name="options">Optional persistence options.</param>
    /// <returns>An in-memory persistence provider.</returns>
    public static IStateMachinePersistence<TState, TTrigger> CreateInMemoryPersistence<TState, TTrigger>(
        PersistenceOptions? options = null)
        where TState : notnull
        where TTrigger : notnull
    {
        return new InMemoryStateMachinePersistence<TState, TTrigger>(options);
    }
}
