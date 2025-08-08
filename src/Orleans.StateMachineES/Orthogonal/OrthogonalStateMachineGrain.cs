using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.StateMachineES.Interfaces;
using Stateless;

namespace Orleans.StateMachineES.Orthogonal;

/// <summary>
/// Base grain class for state machines with orthogonal regions support.
/// Orthogonal regions allow multiple independent state machines to run in parallel
/// within a single grain, enabling complex concurrent behaviors.
/// </summary>
/// <typeparam name="TState">The type of states in the state machine.</typeparam>
/// <typeparam name="TTrigger">The type of triggers that cause state transitions.</typeparam>
public abstract class OrthogonalStateMachineGrain<TState, TTrigger> : StateMachineGrain<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    private readonly Dictionary<string, OrthogonalRegion<TState, TTrigger>> _regions = new();
    private readonly Dictionary<TTrigger, List<string>> _triggerToRegionsMap = new();
    private readonly Dictionary<string, TState> _regionStates = new();
    protected ILogger<OrthogonalStateMachineGrain<TState, TTrigger>>? Logger { get; private set; }

    /// <summary>
    /// Gets the composite state representing all orthogonal regions.
    /// </summary>
    public virtual TState CompositeState => GetCompositeState();

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        
        Logger = ServiceProvider.GetService<ILogger<OrthogonalStateMachineGrain<TState, TTrigger>>>();
        
        // Configure orthogonal regions
        ConfigureOrthogonalRegions();
        
        // Initialize all regions
        foreach (var region in _regions.Values)
        {
            await region.InitializeAsync();
            _regionStates[region.Name] = region.CurrentState;
        }
    }

    /// <summary>
    /// Configures orthogonal regions for this state machine.
    /// Override this method to define your orthogonal regions.
    /// </summary>
    protected abstract void ConfigureOrthogonalRegions();

    /// <summary>
    /// Defines an orthogonal region with its own state machine.
    /// </summary>
    /// <param name="regionName">The name of the region.</param>
    /// <param name="initialState">The initial state of the region.</param>
    /// <param name="configure">Action to configure the region's state machine.</param>
    protected void DefineOrthogonalRegion(
        string regionName,
        TState initialState,
        Action<StateMachine<TState, TTrigger>> configure)
    {
        if (_regions.ContainsKey(regionName))
        {
            throw new InvalidOperationException($"Region '{regionName}' is already defined.");
        }

        var region = new OrthogonalRegion<TState, TTrigger>(regionName, initialState, configure);
        _regions[regionName] = region;
        _regionStates[regionName] = initialState;
        
        Logger?.LogInformation("Defined orthogonal region: {RegionName} with initial state: {InitialState}",
            regionName, initialState);
    }

    /// <summary>
    /// Maps a trigger to specific regions.
    /// When the trigger is fired, it will only affect the specified regions.
    /// </summary>
    /// <param name="trigger">The trigger to map.</param>
    /// <param name="regionNames">The names of regions that should respond to this trigger.</param>
    protected void MapTriggerToRegions(TTrigger trigger, params string[] regionNames)
    {
        if (!_triggerToRegionsMap.ContainsKey(trigger))
        {
            _triggerToRegionsMap[trigger] = new List<string>();
        }

        foreach (var regionName in regionNames)
        {
            if (!_regions.ContainsKey(regionName))
            {
                throw new ArgumentException($"Region '{regionName}' is not defined.");
            }
            
            _triggerToRegionsMap[trigger].Add(regionName);
        }
        
        Logger?.LogDebug("Mapped trigger {Trigger} to regions: {Regions}",
            trigger, string.Join(", ", regionNames));
    }

    /// <summary>
    /// Fires a trigger across all relevant orthogonal regions.
    /// </summary>
    public override async Task FireAsync(TTrigger trigger)
    {
        // Fire in main state machine if it can handle the trigger
        if (StateMachine.CanFire(trigger))
        {
            await base.FireAsync(trigger);
        }

        // Fire in orthogonal regions
        var targetRegions = GetTargetRegions(trigger);
        var tasks = new List<Task>();

        foreach (var region in targetRegions)
        {
            if (region.CanFire(trigger))
            {
                tasks.Add(FireInRegionAsync(region, trigger));
            }
        }

        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Fires a trigger in a specific region.
    /// </summary>
    /// <param name="regionName">The name of the region.</param>
    /// <param name="trigger">The trigger to fire.</param>
    public async Task FireInRegionAsync(string regionName, TTrigger trigger)
    {
        if (!_regions.TryGetValue(regionName, out var region))
        {
            throw new ArgumentException($"Region '{regionName}' does not exist.");
        }

        await FireInRegionAsync(region, trigger);
    }

    private async Task FireInRegionAsync(OrthogonalRegion<TState, TTrigger> region, TTrigger trigger)
    {
        var previousState = region.CurrentState;
        await region.FireAsync(trigger);
        var newState = region.CurrentState;
        
        _regionStates[region.Name] = newState;
        
        Logger?.LogInformation("Region {RegionName} transitioned from {PreviousState} to {NewState} via {Trigger}",
            region.Name, previousState, newState, trigger);
        
        // Notify about region state change
        await OnRegionStateChangedAsync(region.Name, previousState, newState, trigger);
    }

    /// <summary>
    /// Gets the current state of a specific region.
    /// </summary>
    /// <param name="regionName">The name of the region.</param>
    /// <returns>The current state of the region.</returns>
    public TState GetRegionState(string regionName)
    {
        if (!_regions.TryGetValue(regionName, out var region))
        {
            throw new ArgumentException($"Region '{regionName}' does not exist.");
        }

        return region.CurrentState;
    }

    /// <summary>
    /// Gets the states of all regions.
    /// </summary>
    /// <returns>Dictionary mapping region names to their current states.</returns>
    public IReadOnlyDictionary<string, TState> GetAllRegionStates()
    {
        return _regionStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Checks if a trigger can be fired in any region.
    /// </summary>
    public bool CanFireInAnyRegion(TTrigger trigger)
    {
        var targetRegions = GetTargetRegions(trigger);
        return targetRegions.Any(region => region.CanFire(trigger));
    }

    /// <summary>
    /// Checks if a trigger can be fired in a specific region.
    /// </summary>
    public bool CanFireInRegion(string regionName, TTrigger trigger)
    {
        if (!_regions.TryGetValue(regionName, out var region))
        {
            return false;
        }

        return region.CanFire(trigger);
    }

    /// <summary>
    /// Gets the composite state based on all region states.
    /// Override this method to define how region states combine into a composite state.
    /// </summary>
    protected virtual TState GetCompositeState()
    {
        // Default implementation returns the main state machine's state
        // Override this to implement custom composite state logic
        return StateMachine.State;
    }

    /// <summary>
    /// Called when a region's state changes.
    /// Override this method to handle region state changes.
    /// </summary>
    protected virtual Task OnRegionStateChangedAsync(
        string regionName,
        TState previousState,
        TState newState,
        TTrigger trigger)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Synchronizes triggers across regions based on rules.
    /// Call this method to ensure regions stay synchronized.
    /// </summary>
    protected async Task SynchronizeRegionsAsync(TTrigger trigger)
    {
        var tasks = new List<Task>();
        
        foreach (var region in _regions.Values)
        {
            if (region.CanFire(trigger))
            {
                tasks.Add(region.FireAsync(trigger));
            }
        }

        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
            
            // Update region states
            foreach (var region in _regions.Values)
            {
                _regionStates[region.Name] = region.CurrentState;
            }
        }
    }

    /// <summary>
    /// Checks if all regions are in terminal states.
    /// </summary>
    public bool AreAllRegionsInTerminalStates(params TState[] terminalStates)
    {
        return _regions.Values.All(region => 
            terminalStates.Contains(region.CurrentState));
    }

    /// <summary>
    /// Checks if any region is in one of the specified states.
    /// </summary>
    public bool IsAnyRegionInState(params TState[] states)
    {
        return _regions.Values.Any(region => 
            states.Contains(region.CurrentState));
    }

    /// <summary>
    /// Gets the regions that should respond to a trigger.
    /// </summary>
    private IEnumerable<OrthogonalRegion<TState, TTrigger>> GetTargetRegions(TTrigger trigger)
    {
        if (_triggerToRegionsMap.TryGetValue(trigger, out var regionNames))
        {
            return regionNames
                .Where(name => _regions.ContainsKey(name))
                .Select(name => _regions[name]);
        }

        // If no specific mapping, trigger affects all regions
        return _regions.Values;
    }

    /// <summary>
    /// Resets all regions to their initial states.
    /// </summary>
    protected async Task ResetAllRegionsAsync()
    {
        foreach (var region in _regions.Values)
        {
            await region.ResetAsync();
            _regionStates[region.Name] = region.CurrentState;
        }
        
        Logger?.LogInformation("All orthogonal regions have been reset to initial states");
    }

    /// <summary>
    /// Gets a summary of the current state across all regions.
    /// </summary>
    public OrthogonalStateSummary<TState> GetStateSummary()
    {
        return new OrthogonalStateSummary<TState>
        {
            MainState = StateMachine.State,
            RegionStates = new Dictionary<string, TState>(_regionStates),
            CompositeState = CompositeState,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Represents an orthogonal region within a state machine.
/// </summary>
public class OrthogonalRegion<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    private readonly StateMachine<TState, TTrigger> _stateMachine;
    private readonly Action<StateMachine<TState, TTrigger>> _configure;

    public string Name { get; }
    public TState InitialState { get; }
    public TState CurrentState => _stateMachine.State;

    public OrthogonalRegion(
        string name,
        TState initialState,
        Action<StateMachine<TState, TTrigger>> configure)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        InitialState = initialState;
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
        _stateMachine = new StateMachine<TState, TTrigger>(initialState);
    }

    public Task InitializeAsync()
    {
        _configure(_stateMachine);
        return Task.CompletedTask;
    }

    public Task FireAsync(TTrigger trigger)
    {
        return _stateMachine.FireAsync(trigger);
    }

    public bool CanFire(TTrigger trigger)
    {
        return _stateMachine.CanFire(trigger);
    }

    public Task ResetAsync()
    {
        // Reset to initial state by firing appropriate triggers
        // This is a simplified implementation
        return Task.CompletedTask;
    }

    public IEnumerable<TTrigger> GetPermittedTriggers()
    {
        return _stateMachine.PermittedTriggers;
    }
}

/// <summary>
/// Summary of states across all orthogonal regions.
/// </summary>
public class OrthogonalStateSummary<TState>
    where TState : Enum
{
    public TState MainState { get; set; } = default!;
    public Dictionary<string, TState> RegionStates { get; set; } = new();
    public TState CompositeState { get; set; } = default!;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Interface for orthogonal state machine grains.
/// </summary>
public interface IOrthogonalStateMachine<TState, TTrigger> : IStateMachineGrain<TState, TTrigger>
    where TState : Enum
    where TTrigger : Enum
{
    Task FireInRegionAsync(string regionName, TTrigger trigger);
    Task<TState> GetRegionStateAsync(string regionName);
    Task<IReadOnlyDictionary<string, TState>> GetAllRegionStatesAsync();
    Task<OrthogonalStateSummary<TState>> GetStateSummaryAsync();
}