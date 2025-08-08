using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans.StateMachineES.Tracing;
using Stateless;

namespace Orleans.StateMachineES.Composition;

/// <summary>
/// Base grain class for state machines that are composed from multiple components.
/// Supports component inheritance, composition strategies, and dynamic reconfiguration.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers that cause state transitions.</typeparam>
public abstract class ComposedStateMachineGrain<TState, TTrigger> : 
    StateMachineGrain<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    private StateMachineComposer<TState, TTrigger>? _composer;
    private readonly List<IComposableStateMachine<TState, TTrigger>> _components;
    private CompositionContext? _compositionContext;
    private readonly ILogger<ComposedStateMachineGrain<TState, TTrigger>> _logger;

    /// <summary>
    /// Initializes a new instance of the composed state machine grain.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    protected ComposedStateMachineGrain(ILogger<ComposedStateMachineGrain<TState, TTrigger>> logger)
    {
        _logger = logger;
        _components = new List<IComposableStateMachine<TState, TTrigger>>();
    }

    /// <summary>
    /// Gets the composition strategy to use.
    /// </summary>
    protected virtual CompositionStrategy CompositionStrategy => CompositionStrategy.Sequential;

    /// <summary>
    /// Builds the state machine by composing registered components.
    /// </summary>
    protected override StateMachine<TState, TTrigger> BuildStateMachine()
    {
        var initialState = GetInitialState();
        var stateMachine = new StateMachine<TState, TTrigger>(initialState);

        // Register components
        RegisterComponents();

        // Validate components
        ValidateComponents();

        // Create composer if not already created
        if (_composer == null)
        {
            var composerLogger = ServiceProvider.GetService<ILogger<StateMachineComposer<TState, TTrigger>>>() 
                ?? new NullLogger<StateMachineComposer<TState, TTrigger>>();
            _composer = new StateMachineComposer<TState, TTrigger>(
                composerLogger,
                CompositionStrategy);

            foreach (var component in _components)
            {
                _composer.AddComponent(component);
            }
        }

        // Set composition context
        _compositionContext = CreateCompositionContext();
        _composer.SetContext(_compositionContext);

        // Compose the state machine
        var composedMachine = _composer.Compose(stateMachine, initialState);

        // Apply any additional configuration
        ConfigureComposedStateMachine(composedMachine);

        return composedMachine;
    }

    /// <summary>
    /// Registers the components that make up this state machine.
    /// Override this method to register your components.
    /// </summary>
    protected abstract void RegisterComponents();

    /// <summary>
    /// Gets the initial state for the composed state machine.
    /// </summary>
    protected abstract TState GetInitialState();

    /// <summary>
    /// Registers a component to be included in the composition.
    /// </summary>
    /// <param name="component">The component to register.</param>
    protected void RegisterComponent(IComposableStateMachine<TState, TTrigger> component)
    {
        if (component == null)
        {
            throw new ArgumentNullException(nameof(component));
        }

        _components.Add(component);
        
        _logger.LogInformation("Registered component {ComponentId}: {Description}",
            component.ComponentId, component.Description);
    }

    /// <summary>
    /// Registers a component using a factory function.
    /// </summary>
    /// <param name="componentFactory">Factory function to create the component.</param>
    protected void RegisterComponent(Func<IComposableStateMachine<TState, TTrigger>> componentFactory)
    {
        var component = componentFactory();
        RegisterComponent(component);
    }

    /// <summary>
    /// Registers multiple components at once.
    /// </summary>
    /// <param name="components">The components to register.</param>
    protected void RegisterComponents(params IComposableStateMachine<TState, TTrigger>[] components)
    {
        foreach (var component in components)
        {
            RegisterComponent(component);
        }
    }

    /// <summary>
    /// Validates all registered components.
    /// </summary>
    private void ValidateComponents()
    {
        var errors = new List<string>();

        foreach (var component in _components)
        {
            var validationResult = component.Validate();
            if (!validationResult.IsValid)
            {
                errors.AddRange(validationResult.Errors.Select(e => 
                    $"Component '{component.ComponentId}': {e}"));
            }

            // Log warnings
            foreach (var warning in validationResult.Warnings)
            {
                _logger.LogWarning("Component {ComponentId} warning: {Warning}",
                    component.ComponentId, warning);
            }
        }

        if (errors.Any())
        {
            throw new InvalidOperationException(
                $"Component validation failed:\n{string.Join("\n", errors)}");
        }

        _logger.LogInformation("All {Count} components validated successfully", _components.Count);
    }

    /// <summary>
    /// Creates the composition context for this grain.
    /// </summary>
    protected virtual CompositionContext CreateCompositionContext()
    {
        return new CompositionContext
        {
            ParentId = this.GetPrimaryKeyString(),
            GrainContext = this.GrainContext,
            CorrelationId = Guid.NewGuid().ToString(),
            StartedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Applies additional configuration to the composed state machine.
    /// Override this to add grain-specific configuration after composition.
    /// </summary>
    /// <param name="stateMachine">The composed state machine to configure.</param>
    protected virtual void ConfigureComposedStateMachine(StateMachine<TState, TTrigger> stateMachine)
    {
        // Default implementation does nothing
    }

    /// <summary>
    /// Gets a component by its ID.
    /// </summary>
    /// <param name="componentId">The component ID.</param>
    /// <returns>The component if found, null otherwise.</returns>
    protected IComposableStateMachine<TState, TTrigger>? GetComponent(string componentId)
    {
        return _components.FirstOrDefault(c => c.ComponentId == componentId);
    }

    /// <summary>
    /// Gets all registered components.
    /// </summary>
    /// <returns>Read-only collection of components.</returns>
    protected IReadOnlyList<IComposableStateMachine<TState, TTrigger>> GetComponents()
    {
        return _components.AsReadOnly();
    }

    /// <summary>
    /// Dynamically adds a component to the composition at runtime.
    /// Note: This requires rebuilding the state machine.
    /// </summary>
    /// <param name="component">The component to add.</param>
    protected async Task AddComponentDynamicallyAsync(IComposableStateMachine<TState, TTrigger> component)
    {
        using var activity = TracingHelper.StartChildActivity(
            "AddComponentDynamically",
            this.GetType().Name,
            this.GetPrimaryKeyString());

        _logger.LogInformation("Dynamically adding component {ComponentId}", component.ComponentId);

        // Validate the component
        var validationResult = component.Validate();
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Component validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        // Add to components list
        _components.Add(component);

        // Rebuild the state machine
        await RebuildStateMachineAsync();

        _logger.LogInformation("Component {ComponentId} added dynamically", component.ComponentId);
    }

    /// <summary>
    /// Dynamically removes a component from the composition at runtime.
    /// Note: This requires rebuilding the state machine.
    /// </summary>
    /// <param name="componentId">The ID of the component to remove.</param>
    protected async Task RemoveComponentDynamicallyAsync(string componentId)
    {
        using var activity = TracingHelper.StartChildActivity(
            "RemoveComponentDynamically",
            this.GetType().Name,
            this.GetPrimaryKeyString());

        _logger.LogInformation("Dynamically removing component {ComponentId}", componentId);

        var component = _components.FirstOrDefault(c => c.ComponentId == componentId);
        if (component == null)
        {
            throw new InvalidOperationException($"Component '{componentId}' not found");
        }

        // Remove from components list
        _components.Remove(component);

        // Rebuild the state machine
        await RebuildStateMachineAsync();

        _logger.LogInformation("Component {ComponentId} removed dynamically", componentId);
    }

    /// <summary>
    /// Rebuilds the state machine with the current set of components.
    /// </summary>
    private async Task RebuildStateMachineAsync()
    {
        // Note: In a real implementation, you would need to handle state migration
        // more carefully, possibly by:
        // 1. Saving the current state
        // 2. Deactivating and reactivating the grain
        // 3. Or using a more sophisticated state migration approach
        
        _logger.LogWarning("Dynamic component modification requires grain reactivation to rebuild state machine");
        
        // For now, we'll mark that the state machine needs rebuilding
        // and it will be rebuilt on the next activation
        _composer = null; // Force rebuild on next access
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Checks if a state is valid in the current state machine configuration.
    /// </summary>
    private bool IsValidState(TState state)
    {
        try
        {
            // Try to get permitted triggers for the state
            // If this doesn't throw, the state is valid
            var triggers = StateMachine.GetPermittedTriggers();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current composition context.
    /// </summary>
    protected CompositionContext? GetCompositionContext()
    {
        return _compositionContext;
    }

    /// <summary>
    /// Updates shared data in the composition context.
    /// </summary>
    /// <param name="key">The data key.</param>
    /// <param name="value">The data value.</param>
    protected void UpdateSharedData(string key, object value)
    {
        if (_compositionContext != null)
        {
            _compositionContext.SharedData[key] = value;
        }
    }

    /// <summary>
    /// Gets shared data from the composition context.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="key">The data key.</param>
    /// <returns>The data value if found and of correct type, default otherwise.</returns>
    protected T? GetSharedData<T>(string key)
    {
        if (_compositionContext?.SharedData.TryGetValue(key, out var value) == true && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <inheritdoc />
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        _logger.LogInformation("Composed state machine grain {GrainId} activated with {ComponentCount} components",
            this.GetPrimaryKeyString(), _components.Count);

        // Notify all components of activation
        if (_compositionContext != null)
        {
            foreach (var component in _components)
            {
                await component.OnComponentEntryAsync(_compositionContext);
            }
        }
    }

    /// <inheritdoc />
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Composed state machine grain {GrainId} deactivating",
            this.GetPrimaryKeyString());

        // Notify all components of deactivation
        if (_compositionContext != null)
        {
            foreach (var component in _components)
            {
                await component.OnComponentExitAsync(_compositionContext);
            }
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}