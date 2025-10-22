using Microsoft.Extensions.Logging;
using Stateless;

namespace Orleans.StateMachineES.Composition;

/// <summary>
/// Composes multiple state machine components into a single unified state machine.
/// Supports hierarchical composition, state mapping, and trigger forwarding.
/// </summary>
/// <typeparam name="TState">The type of states in the composed state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers in the composed state machine.</typeparam>
/// <remarks>
/// Initializes a new instance of the state machine composer.
/// </remarks>
/// <param name="logger">Logger instance.</param>
/// <param name="strategy">The composition strategy to use.</param>
public class StateMachineComposer<TState, TTrigger>(
    ILogger<StateMachineComposer<TState, TTrigger>> logger,
    CompositionStrategy strategy = CompositionStrategy.Sequential)
    where TState : Enum
    where TTrigger : Enum
{
    private readonly ILogger<StateMachineComposer<TState, TTrigger>> _logger = logger;
    private readonly List<IComposableStateMachine<TState, TTrigger>> _components = [];
    private readonly Dictionary<string, IComposableStateMachine<TState, TTrigger>> _componentMap = [];
    private readonly CompositionStrategy _strategy = strategy;
    private CompositionContext? _currentContext;

    /// <summary>
    /// Adds a component to the composition.
    /// </summary>
    /// <param name="component">The component to add.</param>
    /// <returns>The composer for fluent configuration.</returns>
    public StateMachineComposer<TState, TTrigger> AddComponent(IComposableStateMachine<TState, TTrigger> component)
    {
        if (_componentMap.ContainsKey(component.ComponentId))
        {
            throw new InvalidOperationException($"Component with ID '{component.ComponentId}' already exists");
        }

        var validationResult = component.Validate();
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Component '{component.ComponentId}' validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        _components.Add(component);
        _componentMap[component.ComponentId] = component;

        _logger.LogInformation("Added component {ComponentId} to composition", component.ComponentId);

        return this;
    }

    /// <summary>
    /// Composes all added components into a single state machine.
    /// </summary>
    /// <param name="stateMachine">The state machine to configure.</param>
    /// <param name="initialState">The initial state of the composed machine.</param>
    /// <returns>The configured state machine.</returns>
    public StateMachine<TState, TTrigger> Compose(
        StateMachine<TState, TTrigger> stateMachine,
        TState initialState)
    {
        if (_components.Count == 0)
        {
            throw new InvalidOperationException("No components to compose");
        }

        _logger.LogInformation("Composing {Count} components using {Strategy} strategy",
            _components.Count, _strategy);

        switch (_strategy)
        {
            case CompositionStrategy.Sequential:
                return ComposeSequential(stateMachine, initialState);
            
            case CompositionStrategy.Parallel:
                return ComposeParallel(stateMachine, initialState);
            
            case CompositionStrategy.Hierarchical:
                return ComposeHierarchical(stateMachine, initialState);
            
            case CompositionStrategy.Mixed:
                return ComposeMixed(stateMachine, initialState);
            
            default:
                throw new NotSupportedException($"Composition strategy {_strategy} is not supported");
        }
    }

    /// <summary>
    /// Composes components sequentially, where each component flows into the next.
    /// </summary>
    private StateMachine<TState, TTrigger> ComposeSequential(
        StateMachine<TState, TTrigger> stateMachine,
        TState initialState)
    {
        // Configure each component
        foreach (var component in _components)
        {
            component.Configure(stateMachine);
        }

        // Link components sequentially
        for (int i = 0; i < _components.Count - 1; i++)
        {
            var current = _components[i];
            var next = _components[i + 1];

            foreach (var exitState in current.ExitStates)
            {
                // Create automatic transition from exit states to next component's entry state
                stateMachine.Configure(exitState)
                    .OnExit(async () =>
                    {
                        await current.OnComponentExitAsync(GetOrCreateContext());
                        await next.OnComponentEntryAsync(GetOrCreateContext());
                    });

                // If there's a connecting trigger, configure it
                if (TryGetConnectingTrigger(current, next, out var trigger))
                {
                    stateMachine.Configure(exitState)
                        .Permit(trigger, next.EntryState);
                }
            }
        }

        _logger.LogInformation("Sequential composition completed with {Count} components", _components.Count);

        return stateMachine;
    }

    /// <summary>
    /// Composes components in parallel, allowing multiple components to be active simultaneously.
    /// </summary>
    private StateMachine<TState, TTrigger> ComposeParallel(
        StateMachine<TState, TTrigger> stateMachine,
        TState initialState)
    {
        // This requires a more complex state representation
        // We create composite states that represent combinations of component states

        var parallelStates = StateMachineComposer<TState, TTrigger>.GenerateParallelStates();

        foreach (var component in _components)
        {
            // Configure each component's states
            component.Configure(stateMachine);

            // Add entry/exit handlers
            stateMachine.Configure(component.EntryState)
                .OnEntry(async () => await component.OnComponentEntryAsync(GetOrCreateContext()));

            foreach (var exitState in component.ExitStates)
            {
                stateMachine.Configure(exitState)
                    .OnExit(async () => await component.OnComponentExitAsync(GetOrCreateContext()));
            }
        }

        _logger.LogInformation("Parallel composition completed with {Count} components", _components.Count);

        return stateMachine;
    }

    /// <summary>
    /// Composes components hierarchically, where components can contain other components.
    /// </summary>
    private StateMachine<TState, TTrigger> ComposeHierarchical(
        StateMachine<TState, TTrigger> stateMachine,
        TState initialState)
    {
        // Build hierarchical structure
        var rootComponents = _components.Where(c => !StateMachineComposer<TState, TTrigger>.IsNestedComponent(c)).ToList();

        foreach (var rootComponent in rootComponents)
        {
            ConfigureHierarchicalComponent(stateMachine, rootComponent, null);
        }

        _logger.LogInformation("Hierarchical composition completed with {Count} root components", 
            rootComponents.Count);

        return stateMachine;
    }

    /// <summary>
    /// Composes components using a mixed strategy combining sequential, parallel, and hierarchical.
    /// </summary>
    private StateMachine<TState, TTrigger> ComposeMixed(
        StateMachine<TState, TTrigger> stateMachine,
        TState initialState)
    {
        // This would use metadata or configuration to determine how to compose each component
        // For now, we'll use a simple heuristic-based approach

        var groups = GroupComponentsByType();

        foreach (var group in groups)
        {
            if (group.Value.Count == 1)
            {
                // Single component, just configure it
                group.Value[0].Configure(stateMachine);
            }
            else if (AreComponentsIndependent(group.Value))
            {
                // Independent components, compose in parallel
                ComposeGroupParallel(stateMachine, group.Value);
            }
            else
            {
                // Dependent components, compose sequentially
                ComposeGroupSequential(stateMachine, group.Value);
            }
        }

        _logger.LogInformation("Mixed composition completed with {Count} component groups", groups.Count);

        return stateMachine;
    }

    /// <summary>
    /// Configures a hierarchical component and its children.
    /// </summary>
    private void ConfigureHierarchicalComponent(
        StateMachine<TState, TTrigger> stateMachine,
        IComposableStateMachine<TState, TTrigger> component,
        IComposableStateMachine<TState, TTrigger>? parent)
    {
        // Configure the component itself
        component.Configure(stateMachine);

        // Find child components
        var children = StateMachineComposer<TState, TTrigger>.GetChildComponents(component);

        foreach (var child in children)
        {
            // Configure parent-child relationship
            foreach (var exitState in component.ExitStates)
            {
                if (StateMachineComposer<TState, TTrigger>.CanTransitionToChild(component, child))
                {
                    stateMachine.Configure(exitState)
                        .Permit(StateMachineComposer<TState, TTrigger>.GetTransitionTrigger(component, child), child.EntryState)
                        .OnExit(async () =>
                        {
                            await component.OnComponentExitAsync(GetOrCreateContext());
                            await child.OnComponentEntryAsync(GetOrCreateContext());
                        });
                }
            }

            // Recursively configure children
            ConfigureHierarchicalComponent(stateMachine, child, component);
        }
    }

    /// <summary>
    /// Groups components by their type or category.
    /// </summary>
    private Dictionary<string, List<IComposableStateMachine<TState, TTrigger>>> GroupComponentsByType()
    {
        var groups = new Dictionary<string, List<IComposableStateMachine<TState, TTrigger>>>();

        foreach (var component in _components)
        {
            var type = StateMachineComposer<TState, TTrigger>.GetComponentType(component);
            if (!groups.ContainsKey(type))
            {
                groups[type] = [];
            }
            groups[type].Add(component);
        }

        return groups;
    }

    /// <summary>
    /// Composes a group of components in parallel.
    /// </summary>
    private void ComposeGroupParallel(
        StateMachine<TState, TTrigger> stateMachine,
        List<IComposableStateMachine<TState, TTrigger>> components)
    {
        foreach (var component in components)
        {
            component.Configure(stateMachine);
        }

        // Configure synchronization points if needed
        StateMachineComposer<TState, TTrigger>.ConfigureSynchronizationPoints(stateMachine, components);
    }

    /// <summary>
    /// Composes a group of components sequentially.
    /// </summary>
    private void ComposeGroupSequential(
        StateMachine<TState, TTrigger> stateMachine,
        List<IComposableStateMachine<TState, TTrigger>> components)
    {
        for (int i = 0; i < components.Count; i++)
        {
            components[i].Configure(stateMachine);

            if (i < components.Count - 1)
            {
                LinkComponents(stateMachine, components[i], components[i + 1]);
            }
        }
    }

    /// <summary>
    /// Links two components together.
    /// </summary>
    private void LinkComponents(
        StateMachine<TState, TTrigger> stateMachine,
        IComposableStateMachine<TState, TTrigger> source,
        IComposableStateMachine<TState, TTrigger> target)
    {
        foreach (var exitState in source.ExitStates)
        {
            if (TryGetConnectingTrigger(source, target, out var trigger))
            {
                stateMachine.Configure(exitState)
                    .Permit(trigger, target.EntryState);
            }
        }
    }

    /// <summary>
    /// Configures synchronization points for parallel components.
    /// </summary>
    private static void ConfigureSynchronizationPoints(
        StateMachine<TState, TTrigger> stateMachine,
        List<IComposableStateMachine<TState, TTrigger>> components)
    {
        // This would configure states where parallel components need to synchronize
        // Implementation depends on specific requirements
    }

    /// <summary>
    /// Generates composite states for parallel composition.
    /// </summary>
    private static List<TState> GenerateParallelStates()
    {
        // This would generate composite states that represent
        // combinations of states from different components
        return [];
    }

    /// <summary>
    /// Determines if a component is nested within another.
    /// </summary>
    private static bool IsNestedComponent(IComposableStateMachine<TState, TTrigger> component)
    {
        // Check if this component is referenced as a child by any other component
        return false; // Simplified implementation
    }

    /// <summary>
    /// Gets child components of a parent component.
    /// </summary>
    private static List<IComposableStateMachine<TState, TTrigger>> GetChildComponents(
        IComposableStateMachine<TState, TTrigger> parent)
    {
        // This would use metadata or configuration to determine children
        return [];
    }

    /// <summary>
    /// Determines if components are independent (can run in parallel).
    /// </summary>
    private bool AreComponentsIndependent(List<IComposableStateMachine<TState, TTrigger>> components)
    {
        // Check if components have overlapping states or triggers
        var allStates = new HashSet<TState>();
        var allTriggers = new HashSet<TTrigger>();

        foreach (var component in components)
        {
            var componentStates = StateMachineComposer<TState, TTrigger>.GetComponentStates(component);
            var componentTriggers = StateMachineComposer<TState, TTrigger>.GetComponentTriggers(component);

            if (componentStates.Any(s => allStates.Contains(s)) ||
                componentTriggers.Any(t => allTriggers.Contains(t)))
            {
                return false; // Overlapping states or triggers
            }

            foreach (var state in componentStates)
                allStates.Add(state);
            foreach (var trigger in componentTriggers)
                allTriggers.Add(trigger);
        }

        return true;
    }

    /// <summary>
    /// Gets all states used by a component.
    /// </summary>
    private static HashSet<TState> GetComponentStates(IComposableStateMachine<TState, TTrigger> component)
    {
        var states = new HashSet<TState> { component.EntryState };
        foreach (var exitState in component.ExitStates)
            states.Add(exitState);
        return states;
    }

    /// <summary>
    /// Gets all triggers used by a component.
    /// </summary>
    private static HashSet<TTrigger> GetComponentTriggers(IComposableStateMachine<TState, TTrigger> component)
    {
        return [.. component.MappableTriggers.Values];
    }

    /// <summary>
    /// Gets the type/category of a component.
    /// </summary>
    private static string GetComponentType(IComposableStateMachine<TState, TTrigger> component)
    {
        // This could use attributes or naming conventions
        return component.GetType().Name;
    }

    /// <summary>
    /// Checks if a parent component can transition to a child.
    /// </summary>
    private static bool CanTransitionToChild(
        IComposableStateMachine<TState, TTrigger> parent,
        IComposableStateMachine<TState, TTrigger> child)
    {
        // Check if there's a valid transition path
        return parent.ExitStates.Any() && child.EntryState != null;
    }

    /// <summary>
    /// Gets the trigger for transitioning between components.
    /// </summary>
    private static TTrigger GetTransitionTrigger(
        IComposableStateMachine<TState, TTrigger> source,
        IComposableStateMachine<TState, TTrigger> target)
    {
        // Look for a matching trigger in the mappable triggers
        var transitionKey = $"{source.ComponentId}_to_{target.ComponentId}";
        if (target.MappableTriggers.TryGetValue(transitionKey, out var trigger))
        {
            return trigger;
        }

        // Return a default trigger if available
        if (target.MappableTriggers.TryGetValue("default", out trigger))
        {
            return trigger;
        }

        throw new InvalidOperationException(
            $"No trigger found for transition from {source.ComponentId} to {target.ComponentId}");
    }

    /// <summary>
    /// Tries to get a connecting trigger between two components.
    /// </summary>
    private bool TryGetConnectingTrigger(
        IComposableStateMachine<TState, TTrigger> source,
        IComposableStateMachine<TState, TTrigger> target,
        out TTrigger trigger)
    {
        trigger = default!;
        
        try
        {
            trigger = StateMachineComposer<TState, TTrigger>.GetTransitionTrigger(source, target);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets or creates the current composition context.
    /// </summary>
    private CompositionContext GetOrCreateContext()
    {
        return _currentContext ??= new CompositionContext();
    }

    /// <summary>
    /// Sets the current composition context.
    /// </summary>
    /// <param name="context">The context to set.</param>
    public void SetContext(CompositionContext context)
    {
        _currentContext = context;
    }
}

/// <summary>
/// Strategies for composing state machines.
/// </summary>
public enum CompositionStrategy
{
    /// <summary>
    /// Components are executed one after another.
    /// </summary>
    Sequential,

    /// <summary>
    /// Components can execute simultaneously.
    /// </summary>
    Parallel,

    /// <summary>
    /// Components are nested within parent components.
    /// </summary>
    Hierarchical,

    /// <summary>
    /// Combination of different strategies.
    /// </summary>
    Mixed
}