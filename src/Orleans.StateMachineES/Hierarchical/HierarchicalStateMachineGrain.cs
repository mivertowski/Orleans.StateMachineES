using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans.StateMachineES.Timers;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Timers;
using Stateless;

namespace Orleans.StateMachineES.Hierarchical;

/// <summary>
/// Base grain class for hierarchical state machines that support nested states, sub-states, and parent-child relationships.
/// Extends TimerEnabledStateMachineGrain to provide timer inheritance for hierarchical states.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
/// <typeparam name="TGrainState">The type of the grain state for persistence.</typeparam>
public abstract class HierarchicalStateMachineGrain<TState, TTrigger, TGrainState> : 
    TimerEnabledStateMachineGrain<TState, TTrigger, TGrainState>
    where TGrainState : HierarchicalStateMachineState<TState>, new()
    where TState : notnull
    where TTrigger : notnull
{
    private ILogger<HierarchicalStateMachineGrain<TState, TTrigger, TGrainState>>? _hierarchicalLogger;
    private readonly Dictionary<TState, TState> _stateHierarchy = new();
    private readonly Dictionary<TState, List<TState>> _substates = new();

    /// <inheritdoc/>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        
        _hierarchicalLogger = this.ServiceProvider.GetService<ILogger<HierarchicalStateMachineGrain<TState, TTrigger, TGrainState>>>();
        
        // Configure hierarchical relationships
        ConfigureHierarchy();
        
        // Build hierarchy maps for efficient lookups
        BuildHierarchyMaps();
        
        _hierarchicalLogger?.LogInformation("Hierarchical state machine grain {GrainId} activated with {StateCount} states and {RelationshipCount} hierarchical relationships", 
            this.GetPrimaryKeyString(), _substates.Keys.Count, _stateHierarchy.Count);
    }

    /// <summary>
    /// Configure the hierarchical relationships between states.
    /// Override this method to define parent-child state relationships.
    /// </summary>
    protected abstract void ConfigureHierarchy();

    /// <summary>
    /// Defines a parent-child relationship between states.
    /// </summary>
    /// <param name="substate">The child/sub-state.</param>
    /// <param name="parentState">The parent/super-state.</param>
    protected void DefineSubstate(TState substate, TState parentState)
    {
        _stateHierarchy[substate] = parentState;
        
        if (!_substates.ContainsKey(parentState))
        {
            _substates[parentState] = new List<TState>();
        }
        
        if (!_substates[parentState].Contains(substate))
        {
            _substates[parentState].Add(substate);
        }
        
        _hierarchicalLogger?.LogDebug("Defined hierarchical relationship: {Substate} is substate of {ParentState}", 
            substate, parentState);
    }

    /// <summary>
    /// Gets the parent state of the specified state, if any.
    /// </summary>
    /// <param name="state">The state to get the parent for.</param>
    /// <returns>The parent state, or default if the state has no parent.</returns>
    public virtual Task<TState?> GetParentStateAsync(TState state)
    {
        var parentState = _stateHierarchy.TryGetValue(state, out var parent) ? parent : default(TState?);
        return Task.FromResult(parentState);
    }

    /// <summary>
    /// Gets all direct child states of the specified state.
    /// </summary>
    /// <param name="parentState">The parent state to get children for.</param>
    /// <returns>Collection of direct child states.</returns>
    public Task<IReadOnlyList<TState>> GetSubstatesAsync(TState parentState)
    {
        var substates = _substates.TryGetValue(parentState, out var children)
            ? children.AsReadOnly()
            : (IReadOnlyList<TState>)[];
        return Task.FromResult(substates);
    }

    /// <summary>
    /// Gets all ancestor states of the specified state (parent, grandparent, etc.).
    /// </summary>
    /// <param name="state">The state to get ancestors for.</param>
    /// <returns>Collection of ancestor states from direct parent to root.</returns>
    public Task<IReadOnlyList<TState>> GetAncestorStatesAsync(TState state)
    {
        var ancestors = new List<TState>();
        var currentState = state;

        while (_stateHierarchy.TryGetValue(currentState, out var parent))
        {
            ancestors.Add(parent);
            currentState = parent;
        }

        return Task.FromResult<IReadOnlyList<TState>>(ancestors.AsReadOnly());
    }

    /// <summary>
    /// Gets all descendant states of the specified state (children, grandchildren, etc.).
    /// </summary>
    /// <param name="parentState">The parent state to get descendants for.</param>
    /// <returns>Collection of all descendant states.</returns>
    public Task<IReadOnlyList<TState>> GetDescendantStatesAsync(TState parentState)
    {
        var descendants = new List<TState>();
        CollectDescendants(parentState, descendants);
        return Task.FromResult<IReadOnlyList<TState>>(descendants.AsReadOnly());
    }

    /// <summary>
    /// Checks if the state machine is currently in the specified state or any of its substates.
    /// This extends the base IsInState functionality to include hierarchical checking.
    /// </summary>
    /// <param name="state">The state to check.</param>
    /// <returns>True if the state machine is in the specified state or any of its substates.</returns>
    public async Task<bool> IsInStateOrSubstateAsync(TState state)
    {
        // Check if directly in the state
        if (await IsInStateAsync(state))
        {
            return true;
        }

        // Check if in any substate
        var substates = await GetDescendantStatesAsync(state);
        foreach (var substate in substates)
        {
            if (await IsInStateAsync(substate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the hierarchy path from root to the current state.
    /// </summary>
    /// <returns>The complete path from root state to current state.</returns>
    public async Task<IReadOnlyList<TState>> GetCurrentStatePathAsync()
    {
        var currentState = await GetStateAsync();
        var path = new List<TState> { currentState };
        
        var ancestors = await GetAncestorStatesAsync(currentState);
        path.AddRange(ancestors);
        path.Reverse(); // Root to current
        
        return path.AsReadOnly();
    }

    /// <summary>
    /// Gets the deepest active substate for a given parent state.
    /// </summary>
    /// <param name="parentState">The parent state to check.</param>
    /// <returns>The deepest active substate, or default if not in this hierarchy branch.</returns>
    public virtual async Task<TState?> GetActiveSubstateAsync(TState parentState)
    {
        var currentState = await GetStateAsync();
        
        // Check if current state is a descendant of the parent state
        var ancestors = await GetAncestorStatesAsync(currentState);
        if (!ancestors.Contains(parentState) && !EqualityComparer<TState>.Default.Equals(currentState, parentState))
        {
            return default(TState?);
        }

        return currentState;
    }

    /// <summary>
    /// Builds internal hierarchy maps for efficient lookups.
    /// </summary>
    private void BuildHierarchyMaps()
    {
        _hierarchicalLogger?.LogDebug("Built hierarchy maps: {StateHierarchyCount} parent-child relationships, {SubstateCount} parent states with children",
            _stateHierarchy.Count, _substates.Count);
    }

    /// <summary>
    /// Recursively collects all descendant states.
    /// </summary>
    private void CollectDescendants(TState parentState, List<TState> descendants)
    {
        if (!_substates.TryGetValue(parentState, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            descendants.Add(child);
            CollectDescendants(child, descendants); // Recursive for grandchildren
        }
    }

    /// <summary>
    /// Override to provide hierarchical context in transition events.
    /// </summary>
    protected override async Task RecordTransitionEvent(TState fromState, TState toState, TTrigger trigger, string? dedupeKey, Dictionary<string, object>? metadata = null)
    {
        // Enhance metadata with hierarchical information
        metadata ??= new Dictionary<string, object>();
        
        var fromAncestors = await GetAncestorStatesAsync(fromState);
        var toAncestors = await GetAncestorStatesAsync(toState);
        
        metadata["FromStateAncestors"] = fromAncestors.ToList();
        metadata["ToStateAncestors"] = toAncestors.ToList();
        metadata["IsHierarchicalTransition"] = fromAncestors.Any() || toAncestors.Any();
        
        // Determine if this is a transition within the same hierarchy branch
        var sharedAncestors = fromAncestors.Intersect(toAncestors).ToList();
        metadata["SharedAncestors"] = sharedAncestors;
        metadata["HierarchyChangeLevel"] = sharedAncestors.Count;
        
        _hierarchicalLogger?.LogInformation("Hierarchical transition: {FromState} -> {ToState} (shared ancestors: {SharedCount})", 
            fromState, toState, sharedAncestors.Count);

        await base.RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata);
    }

    /// <summary>
    /// Gets hierarchical state information for debugging and inspection.
    /// </summary>
    public Task<HierarchicalStateInfo<TState>> GetHierarchicalInfoAsync()
    {
        // Root states are those that appear as parent states but are not themselves substates
        // Plus any states that are neither parents nor children (isolated states)
        var allReferencedStates = new HashSet<TState>(_stateHierarchy.Keys); // All substates
        allReferencedStates.UnionWith(_stateHierarchy.Values); // All parent states
        
        var rootStates = allReferencedStates.Except(_stateHierarchy.Keys).ToList(); // Parent states that are not substates
        
        var info = new HierarchicalStateInfo<TState>
        {
            StateHierarchy = new Dictionary<TState, TState>(_stateHierarchy),
            Substates = _substates.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TState>)kvp.Value.AsReadOnly()),
            RootStates = rootStates.AsReadOnly()
        };
        
        return Task.FromResult(info);
    }
}

/// <summary>
/// Base state class for hierarchical state machines with hierarchy tracking.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
[GenerateSerializer]
public class HierarchicalStateMachineState<TState> : TimerEnabledStateMachineState<TState>
{
    /// <summary>
    /// The current active path in the state hierarchy.
    /// </summary>
    [Id(0)]
    public List<TState> CurrentHierarchyPath { get; set; } = new();
}

/// <summary>
/// Information about the hierarchical structure of a state machine.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
[GenerateSerializer]
public class HierarchicalStateInfo<TState> where TState : notnull
{
    /// <summary>
    /// Maps each substate to its parent state.
    /// </summary>
    [Id(0)]
    public Dictionary<TState, TState> StateHierarchy { get; set; } = new();

    /// <summary>
    /// Maps each parent state to its direct children.
    /// </summary>
    [Id(1)]
    public Dictionary<TState, IReadOnlyList<TState>> Substates { get; set; } = new();

    /// <summary>
    /// Root states that have no parent.
    /// </summary>
    [Id(2)]
    public IReadOnlyList<TState> RootStates { get; set; } = [];
}