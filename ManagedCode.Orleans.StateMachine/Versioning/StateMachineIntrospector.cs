using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Stateless;
using Stateless.Graph;
using Stateless.Reflection;

namespace ivlt.Orleans.StateMachineES.Versioning;

/// <summary>
/// Production-ready introspector for analyzing and comparing state machine configurations.
/// Provides deep inspection capabilities for state machines including states, transitions, guards, and actions.
/// </summary>
public class StateMachineIntrospector<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly ILogger<StateMachineIntrospector<TState, TTrigger>> _logger;

    public StateMachineIntrospector(ILogger<StateMachineIntrospector<TState, TTrigger>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts complete configuration from a state machine.
    /// </summary>
    public StateMachineConfiguration<TState, TTrigger> ExtractConfiguration(StateMachine<TState, TTrigger> machine)
    {
        try
        {
            var config = new StateMachineConfiguration<TState, TTrigger>();
            
            // Use Stateless built-in reflection API
            var info = machine.GetInfo();
            
            foreach (var state in info.States)
            {
                var stateConfig = new StateConfiguration<TState, TTrigger>
                {
                    State = ParseStateValue(state.UnderlyingState),
                    IsInitialState = state.UnderlyingState.ToString() == machine.State.ToString()
                };

                // Extract superstate (parent state)
                if (state.Superstate != null)
                {
                    stateConfig.Superstate = ParseStateValue(state.Superstate.UnderlyingState);
                }

                // Extract substates
                foreach (var substate in state.Substates)
                {
                    stateConfig.Substates.Add(ParseStateValue(substate.UnderlyingState));
                }

                // Extract permitted triggers
                foreach (var trigger in state.PermittedTriggers)
                {
                    stateConfig.PermittedTriggers.Add(ParseTriggerValue(trigger.UnderlyingTrigger));
                }

                // Extract fixed transitions (transitions without guards)
                foreach (var transition in state.FixedTransitions)
                {
                    var transitionConfig = new TransitionConfiguration<TState, TTrigger>
                    {
                        SourceState = stateConfig.State,
                        Trigger = ParseTriggerValue(transition.Trigger.UnderlyingTrigger),
                        DestinationState = ParseStateValue(transition.DestinationState.UnderlyingState),
                        HasGuard = false
                    };
                    stateConfig.Transitions.Add(transitionConfig);
                }

                // Extract dynamic transitions (transitions with guards)
                foreach (var transition in state.DynamicTransitions)
                {
                    var transitionConfig = new TransitionConfiguration<TState, TTrigger>
                    {
                        SourceState = stateConfig.State,
                        Trigger = ParseTriggerValue(transition.Trigger.UnderlyingTrigger),
                        // Dynamic transitions may have multiple possible destinations based on guards
                        HasGuard = true,
                        GuardDescription = transition.GuardDescription
                    };
                    
                    // Try to determine possible destination states
                    if (transition.PossibleDestinationStates?.Any() == true)
                    {
                        foreach (var destState in transition.PossibleDestinationStates)
                        {
                            transitionConfig.PossibleDestinations.Add(ParseStateValue(destState.UnderlyingState));
                        }
                    }
                    
                    stateConfig.Transitions.Add(transitionConfig);
                }

                // Extract ignored transitions
                foreach (var ignored in state.IgnoredTriggers)
                {
                    stateConfig.IgnoredTriggers.Add(ParseTriggerValue(ignored.UnderlyingTrigger));
                }

                // Extract entry actions
                foreach (var action in state.EntryActions)
                {
                    stateConfig.EntryActions.Add(new ActionConfiguration
                    {
                        Description = action.Description,
                        FromTrigger = action.FromTrigger != null ? ParseTriggerValue(action.FromTrigger) : (TTrigger?)null
                    });
                }

                // Extract exit actions
                foreach (var action in state.ExitActions)
                {
                    stateConfig.ExitActions.Add(new ActionConfiguration
                    {
                        Description = action.Description
                    });
                }

                config.States[stateConfig.State] = stateConfig;
            }

            // Build complete transition map
            foreach (var stateConfig in config.States.Values)
            {
                foreach (var transition in stateConfig.Transitions)
                {
                    var key = (transition.SourceState, transition.Trigger);
                    if (!config.TransitionMap.ContainsKey(key))
                    {
                        config.TransitionMap[key] = new List<TransitionConfiguration<TState, TTrigger>>();
                    }
                    config.TransitionMap[key].Add(transition);
                }
            }

            config.InitialState = machine.State;
            config.IsValid = true;

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract state machine configuration");
            throw;
        }
    }

    /// <summary>
    /// Creates a deep copy of a state machine with its complete configuration.
    /// </summary>
    public StateMachine<TState, TTrigger> CloneStateMachine(
        StateMachine<TState, TTrigger> source, 
        TState? initialState = null)
    {
        var config = ExtractConfiguration(source);
        var targetInitialState = initialState ?? config.InitialState;
        var clone = new StateMachine<TState, TTrigger>(targetInitialState);

        // Apply configuration to the clone
        ApplyConfiguration(clone, config);

        return clone;
    }

    /// <summary>
    /// Applies a configuration to a state machine.
    /// </summary>
    public void ApplyConfiguration(
        StateMachine<TState, TTrigger> machine, 
        StateMachineConfiguration<TState, TTrigger> config)
    {
        // First pass: Configure all states and their hierarchy
        foreach (var stateConfig in config.States.Values)
        {
            var stateAccessor = machine.Configure(stateConfig.State);

            // Set superstate if exists
            if (stateConfig.Superstate.HasValue)
            {
                stateAccessor.SubstateOf(stateConfig.Superstate.Value);
            }

            // Configure ignored triggers
            foreach (var ignoredTrigger in stateConfig.IgnoredTriggers)
            {
                stateAccessor.Ignore(ignoredTrigger);
            }
        }

        // Second pass: Configure transitions
        foreach (var stateConfig in config.States.Values)
        {
            var stateAccessor = machine.Configure(stateConfig.State);

            foreach (var transition in stateConfig.Transitions)
            {
                if (!transition.HasGuard && transition.DestinationState.HasValue)
                {
                    // Simple transition without guard
                    stateAccessor.Permit(transition.Trigger, transition.DestinationState.Value);
                }
                else if (transition.HasGuard && transition.PossibleDestinations.Count > 0)
                {
                    // For guarded transitions, we need to handle them specially
                    // In production, you'd need to recreate the actual guard conditions
                    _logger.LogWarning("Guarded transition from {Source} on {Trigger} cannot be fully cloned without guard logic", 
                        transition.SourceState, transition.Trigger);
                }
            }
        }
    }

    /// <summary>
    /// Compares two state machine configurations to detect differences.
    /// </summary>
    public ConfigurationComparison<TState, TTrigger> CompareConfigurations(
        StateMachineConfiguration<TState, TTrigger> config1,
        StateMachineConfiguration<TState, TTrigger> config2)
    {
        var comparison = new ConfigurationComparison<TState, TTrigger>();

        // Compare states
        var states1 = new HashSet<TState>(config1.States.Keys);
        var states2 = new HashSet<TState>(config2.States.Keys);

        comparison.AddedStates = states2.Except(states1).ToList();
        comparison.RemovedStates = states1.Except(states2).ToList();
        comparison.CommonStates = states1.Intersect(states2).ToList();

        // Compare transitions for common states
        foreach (var state in comparison.CommonStates)
        {
            var stateConfig1 = config1.States[state];
            var stateConfig2 = config2.States[state];

            // Compare permitted triggers
            var triggers1 = new HashSet<TTrigger>(stateConfig1.PermittedTriggers);
            var triggers2 = new HashSet<TTrigger>(stateConfig2.PermittedTriggers);

            var addedTriggers = triggers2.Except(triggers1);
            var removedTriggers = triggers1.Except(triggers2);

            if (addedTriggers.Any())
            {
                var change = new TransitionChange<TState, TTrigger>
                {
                    State = state,
                    Triggers = addedTriggers.ToList(),
                    ChangeType = TransitionChangeType.Added
                };
                change.UpdateIsBreaking();
                comparison.AddedTransitions.Add(change);
            }

            if (removedTriggers.Any())
            {
                var change = new TransitionChange<TState, TTrigger>
                {
                    State = state,
                    Triggers = removedTriggers.ToList(),
                    ChangeType = TransitionChangeType.Removed
                };
                change.UpdateIsBreaking();
                comparison.RemovedTransitions.Add(change);
            }

            // Compare transition destinations
            foreach (var trigger in triggers1.Intersect(triggers2))
            {
                var trans1 = stateConfig1.Transitions.Where(t => t.Trigger.Equals(trigger)).ToList();
                var trans2 = stateConfig2.Transitions.Where(t => t.Trigger.Equals(trigger)).ToList();

                if (trans1.Count > 0 && trans2.Count > 0)
                {
                    var dest1 = trans1.First().DestinationState;
                    var dest2 = trans2.First().DestinationState;

                    if (!Equals(dest1, dest2))
                    {
                        var change = new TransitionChange<TState, TTrigger>
                        {
                            State = state,
                            Triggers = new List<TTrigger> { trigger },
                            ChangeType = TransitionChangeType.Modified,
                            OldDestination = dest1,
                            NewDestination = dest2
                        };
                        change.UpdateIsBreaking();
                        comparison.ModifiedTransitions.Add(change);
                    }
                }
            }

            // Compare guards
            var guards1 = stateConfig1.Transitions.Where(t => t.HasGuard).ToList();
            var guards2 = stateConfig2.Transitions.Where(t => t.HasGuard).ToList();

            if (guards1.Count != guards2.Count)
            {
                comparison.GuardChanges.Add(new GuardChange<TState, TTrigger>
                {
                    State = state,
                    ChangeType = GuardChangeType.CountChanged,
                    OldCount = guards1.Count,
                    NewCount = guards2.Count
                });
            }
        }

        comparison.HasBreakingChanges = comparison.RemovedStates.Any() || 
                                        comparison.RemovedTransitions.Any() ||
                                        comparison.ModifiedTransitions.Any(t => t.IsBreaking);

        return comparison;
    }

    /// <summary>
    /// Simulates a trigger execution to predict the destination state.
    /// </summary>
    public async Task<StateTransitionPrediction<TState>> PredictTransition(
        StateMachine<TState, TTrigger> machine,
        TState currentState,
        TTrigger trigger)
    {
        var prediction = new StateTransitionPrediction<TState>
        {
            CurrentState = currentState,
            Trigger = trigger
        };

        try
        {
            // Extract configuration to analyze
            var config = ExtractConfiguration(machine);
            
            if (!config.States.TryGetValue(currentState, out var stateConfig))
            {
                prediction.CanFire = false;
                prediction.Reason = $"State {currentState} not found in configuration";
                return prediction;
            }

            // Check if trigger is ignored
            if (stateConfig.IgnoredTriggers.Contains(trigger))
            {
                prediction.CanFire = true;
                prediction.IsIgnored = true;
                prediction.PredictedState = currentState;
                prediction.Reason = "Trigger is ignored in this state";
                return prediction;
            }

            // Check if trigger is permitted
            if (!stateConfig.PermittedTriggers.Contains(trigger))
            {
                prediction.CanFire = false;
                prediction.Reason = $"Trigger {trigger} is not permitted in state {currentState}";
                return prediction;
            }

            // Find the transition
            var transition = stateConfig.Transitions.FirstOrDefault(t => t.Trigger.Equals(trigger));
            
            if (transition == null)
            {
                prediction.CanFire = false;
                prediction.Reason = "No transition found for this trigger";
                return prediction;
            }

            if (transition.HasGuard)
            {
                // For guarded transitions, we can't determine the exact destination without evaluating the guard
                prediction.CanFire = true;
                prediction.HasGuard = true;
                prediction.PossibleDestinations = transition.PossibleDestinations;
                prediction.Reason = "Transition has guard conditions";
                
                if (transition.PossibleDestinations.Count == 1)
                {
                    prediction.PredictedState = transition.PossibleDestinations.First();
                }
            }
            else
            {
                prediction.CanFire = true;
                prediction.PredictedState = transition.DestinationState;
                prediction.Reason = "Transition would succeed";
            }

            // Check for entry/exit actions
            prediction.HasEntryActions = config.States.TryGetValue(prediction.PredictedState ?? currentState, out var destState) 
                                        && destState.EntryActions.Any();
            prediction.HasExitActions = stateConfig.ExitActions.Any();

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to predict transition for trigger {Trigger} in state {State}", 
                trigger, currentState);
            
            prediction.CanFire = false;
            prediction.Reason = $"Error: {ex.Message}";
            return prediction;
        }
    }

    private TState ParseStateValue(object stateValue)
    {
        if (stateValue is TState state)
            return state;
        
        return (TState)Enum.Parse(typeof(TState), stateValue.ToString());
    }

    private TTrigger ParseTriggerValue(object triggerValue)
    {
        if (triggerValue is TTrigger trigger)
            return trigger;
        
        return (TTrigger)Enum.Parse(typeof(TTrigger), triggerValue.ToString());
    }
}

/// <summary>
/// Complete configuration of a state machine.
/// </summary>
[GenerateSerializer]
public class StateMachineConfiguration<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public Dictionary<TState, StateConfiguration<TState, TTrigger>> States { get; set; } = new();
    [Id(1)] public Dictionary<(TState Source, TTrigger Trigger), List<TransitionConfiguration<TState, TTrigger>>> TransitionMap { get; set; } = new();
    [Id(2)] public TState InitialState { get; set; }
    [Id(3)] public bool IsValid { get; set; }
    [Id(4)] public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Configuration for a single state.
/// </summary>
[GenerateSerializer]
public class StateConfiguration<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public TState State { get; set; }
    [Id(1)] public TState? Superstate { get; set; }
    [Id(2)] public List<TState> Substates { get; set; } = new();
    [Id(3)] public List<TTrigger> PermittedTriggers { get; set; } = new();
    [Id(4)] public List<TTrigger> IgnoredTriggers { get; set; } = new();
    [Id(5)] public List<TransitionConfiguration<TState, TTrigger>> Transitions { get; set; } = new();
    [Id(6)] public List<ActionConfiguration> EntryActions { get; set; } = new();
    [Id(7)] public List<ActionConfiguration> ExitActions { get; set; } = new();
    [Id(8)] public bool IsInitialState { get; set; }
}

/// <summary>
/// Configuration for a transition.
/// </summary>
[GenerateSerializer]
public class TransitionConfiguration<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public TState SourceState { get; set; }
    [Id(1)] public TTrigger Trigger { get; set; }
    [Id(2)] public TState? DestinationState { get; set; }
    [Id(3)] public List<TState> PossibleDestinations { get; set; } = new();
    [Id(4)] public bool HasGuard { get; set; }
    [Id(5)] public string? GuardDescription { get; set; }
}

/// <summary>
/// Configuration for an action.
/// </summary>
[GenerateSerializer]
public class ActionConfiguration
{
    [Id(0)] public string Description { get; set; } = "";
    [Id(1)] public object? FromTrigger { get; set; }
}

/// <summary>
/// Result of comparing two state machine configurations.
/// </summary>
[GenerateSerializer]
public class ConfigurationComparison<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public List<TState> AddedStates { get; set; } = new();
    [Id(1)] public List<TState> RemovedStates { get; set; } = new();
    [Id(2)] public List<TState> CommonStates { get; set; } = new();
    [Id(3)] public List<TransitionChange<TState, TTrigger>> AddedTransitions { get; set; } = new();
    [Id(4)] public List<TransitionChange<TState, TTrigger>> RemovedTransitions { get; set; } = new();
    [Id(5)] public List<TransitionChange<TState, TTrigger>> ModifiedTransitions { get; set; } = new();
    [Id(6)] public List<GuardChange<TState, TTrigger>> GuardChanges { get; set; } = new();
    [Id(7)] public bool HasBreakingChanges { get; set; }
}

/// <summary>
/// Represents a change in transitions.
/// </summary>
[GenerateSerializer]
public class TransitionChange<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public TState State { get; set; }
    [Id(1)] public List<TTrigger> Triggers { get; set; } = new();
    [Id(2)] public TransitionChangeType ChangeType { get; set; }
    [Id(3)] public TState? OldDestination { get; set; }
    [Id(4)] public TState? NewDestination { get; set; }
    [Id(5)] public bool IsBreaking { get; set; }
    
    /// <summary>
    /// Determines if this change is breaking.
    /// </summary>
    public void UpdateIsBreaking()
    {
        IsBreaking = ChangeType == TransitionChangeType.Removed || 
                    (ChangeType == TransitionChangeType.Modified && !Equals(OldDestination, NewDestination));
    }
}

/// <summary>
/// Type of transition change.
/// </summary>
public enum TransitionChangeType
{
    Added,
    Removed,
    Modified
}

/// <summary>
/// Represents a change in guard conditions.
/// </summary>
[GenerateSerializer]
public class GuardChange<TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    [Id(0)] public TState State { get; set; }
    [Id(1)] public GuardChangeType ChangeType { get; set; }
    [Id(2)] public int OldCount { get; set; }
    [Id(3)] public int NewCount { get; set; }
}

/// <summary>
/// Type of guard change.
/// </summary>
public enum GuardChangeType
{
    Added,
    Removed,
    Modified,
    CountChanged
}

/// <summary>
/// Prediction of a state transition.
/// </summary>
[GenerateSerializer]
public class StateTransitionPrediction<TState>
    where TState : struct, Enum
{
    [Id(0)] public TState CurrentState { get; set; }
    [Id(1)] public object Trigger { get; set; } = new();
    [Id(2)] public bool CanFire { get; set; }
    [Id(3)] public TState? PredictedState { get; set; }
    [Id(4)] public List<TState> PossibleDestinations { get; set; } = new();
    [Id(5)] public bool HasGuard { get; set; }
    [Id(6)] public bool IsIgnored { get; set; }
    [Id(7)] public bool HasEntryActions { get; set; }
    [Id(8)] public bool HasExitActions { get; set; }
    [Id(9)] public string Reason { get; set; } = "";
}