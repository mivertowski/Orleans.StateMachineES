using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using ivlt.Orleans.StateMachineES.EventSourcing.Events;
using ivlt.Orleans.StateMachineES.EventSourcing.Configuration;
using ivlt.Orleans.StateMachineES.Interfaces;
using ivlt.Orleans.StateMachineES.Models;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Streams;
using Stateless;
using Stateless.Graph;

namespace ivlt.Orleans.StateMachineES.EventSourcing;

/// <summary>
/// Base grain class for event-sourced state machines that automatically persists state transitions as events.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
/// <typeparam name="TGrainState">The type of the grain state (for snapshotting).</typeparam>
public abstract class EventSourcedStateMachineGrain<TState, TTrigger, TGrainState> : 
    JournaledGrain<TGrainState>, 
    IStateMachineGrain<TState, TTrigger>
    where TGrainState : EventSourcedStateMachineState<TState>, new()
    where TState : notnull
    where TTrigger : notnull
{
    [NotNull]
    protected StateMachine<TState, TTrigger>? StateMachine { get; private set; }

    protected EventSourcingOptions Options { get; private set; } = new();

    private readonly HashSet<string> _processedDedupeKeys = new();
    private string? _currentCorrelationId;

    /// <summary>
    /// Activates the state machine.
    /// </summary>
    public Task ActivateAsync()
    {
        return StateMachine.ActivateAsync();
    }

    /// <summary>
    /// Deactivates the state machine.
    /// </summary>
    public Task DeactivateAsync()
    {
        return StateMachine.DeactivateAsync();
    }

    /// <summary>
    /// Fires the specified trigger asynchronously with event sourcing.
    /// </summary>
    public async Task FireAsync(TTrigger trigger)
    {
        var dedupeKey = GenerateDedupeKey(trigger);
        if (!ShouldProcessTrigger(dedupeKey))
        {
            return; // Idempotent - already processed
        }

        var fromState = StateMachine.State;
        await StateMachine.FireAsync(trigger);
        var toState = StateMachine.State;

        await RecordTransitionEvent(fromState, toState, trigger, dedupeKey);
    }

    /// <summary>
    /// Fires the specified trigger with one argument asynchronously.
    /// </summary>
    public async Task FireAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var dedupeKey = GenerateDedupeKey(trigger, arg0);
        if (!ShouldProcessTrigger(dedupeKey))
        {
            return;
        }

        var fromState = StateMachine.State;
        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        await StateMachine.FireAsync(tp, arg0);
        var toState = StateMachine.State;

        await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, new Dictionary<string, object> { ["arg0"] = arg0! });
    }

    /// <summary>
    /// Fires the specified trigger with two arguments asynchronously.
    /// </summary>
    public async Task FireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var dedupeKey = GenerateDedupeKey(trigger, arg0, arg1);
        if (!ShouldProcessTrigger(dedupeKey))
        {
            return;
        }

        var fromState = StateMachine.State;
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        await StateMachine.FireAsync(tp, arg0, arg1);
        var toState = StateMachine.State;

        await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, 
            new Dictionary<string, object> { ["arg0"] = arg0!, ["arg1"] = arg1! });
    }

    /// <summary>
    /// Fires the specified trigger with three arguments asynchronously.
    /// </summary>
    public async Task FireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var dedupeKey = GenerateDedupeKey(trigger, arg0, arg1, arg2);
        if (!ShouldProcessTrigger(dedupeKey))
        {
            return;
        }

        var fromState = StateMachine.State;
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        await StateMachine.FireAsync(tp, arg0, arg1, arg2);
        var toState = StateMachine.State;

        await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, 
            new Dictionary<string, object> { ["arg0"] = arg0!, ["arg1"] = arg1!, ["arg2"] = arg2! });
    }

    /// <summary>
    /// Records a state transition event.
    /// </summary>
    protected virtual async Task RecordTransitionEvent(
        TState fromState, 
        TState toState, 
        TTrigger trigger, 
        string? dedupeKey,
        Dictionary<string, object>? metadata = null)
    {
        var transitionEvent = new StateTransitionEvent<TState, TTrigger>(
            fromState,
            toState,
            trigger,
            DateTime.UtcNow,
            _currentCorrelationId,
            dedupeKey,
            GetStateMachineVersion(),
            metadata
        );

        // Raise event for persistence
        RaiseEvent(transitionEvent);

        // Update grain state
        State.CurrentState = toState;
        State.LastTransitionTimestamp = transitionEvent.Timestamp;
        State.TransitionCount++;

        // Auto-confirm if configured
        if (Options.AutoConfirmEvents)
        {
            await ConfirmEvents();
        }

        // Publish to stream if configured
        if (Options.PublishToStream && !string.IsNullOrEmpty(Options.StreamProvider))
        {
            await PublishToStream(transitionEvent);
        }
    }

    /// <summary>
    /// Publishes an event to Orleans Streams.
    /// </summary>
    protected virtual async Task PublishToStream(StateTransitionEvent<TState, TTrigger> evt)
    {
        try
        {
            var streamProvider = this.GetStreamProvider(Options.StreamProvider!);
            var streamId = StreamId.Create(Options.StreamNamespace ?? "StateMachine", this.GetPrimaryKeyString());
            var stream = streamProvider.GetStream<StateTransitionEvent<TState, TTrigger>>(streamId);
            await stream.OnNextAsync(evt);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the transition
            // In production, you'd want proper logging here
            Console.WriteLine($"Failed to publish event to stream: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current state asynchronously.
    /// </summary>
    public Task<TState> GetStateAsync()
    {
        return Task.FromResult(StateMachine.State);
    }

    /// <summary>
    /// Determines whether the state machine is in the specified state.
    /// </summary>
    public Task<bool> IsInStateAsync(TState state)
    {
        return Task.FromResult(StateMachine.IsInState(state));
    }

    /// <summary>
    /// Determines whether the specified trigger can be fired.
    /// </summary>
    public Task<bool> CanFireAsync(TTrigger trigger)
    {
        return Task.FromResult(StateMachine.CanFire(trigger));
    }

    /// <summary>
    /// Gets information about the state machine.
    /// </summary>
    public Task<OrleansStateMachineInfo> GetInfoAsync()
    {
        return Task.FromResult(new OrleansStateMachineInfo(StateMachine.GetInfo()));
    }

    /// <summary>
    /// Gets the permitted triggers for the current state.
    /// </summary>
    public Task<IEnumerable<TTrigger>> GetPermittedTriggersAsync(params object[] args)
    {
        return Task.FromResult(StateMachine.GetPermittedTriggers(args));
    }

    /// <summary>
    /// Gets detailed information about permitted triggers for the current state.
    /// </summary>
    public Task<IEnumerable<TriggerDetails<TState, TTrigger>>> GetDetailedPermittedTriggersAsync(params object[] args)
    {
        return Task.FromResult(StateMachine.GetDetailedPermittedTriggers(args));
    }

    /// <summary>
    /// Gets the permitted triggers property for the current state.
    /// </summary>
    public Task<IEnumerable<TTrigger>> GetPermittedTriggersPropertyAsync()
    {
        return Task.FromResult(StateMachine.PermittedTriggers);
    }

    /// <summary>
    /// Determines whether the specified trigger can be fired and returns unmet guard conditions.
    /// </summary>
    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync(TTrigger trigger)
    {
        var result = StateMachine.CanFire(trigger, out var unmetGuards);
        return Task.FromResult((result, unmetGuards));
    }

    /// <summary>
    /// Determines whether the specified trigger with one argument can be fired.
    /// </summary>
    public Task<bool> CanFireAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        return Task.FromResult(StateMachine.CanFire(tp, arg0));
    }

    /// <summary>
    /// Determines whether the specified trigger with one argument can be fired and returns unmet guard conditions.
    /// </summary>
    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        var result = StateMachine.CanFire(tp, arg0, out var unmet);
        return Task.FromResult((result, unmet));
    }

    /// <summary>
    /// Determines whether the specified trigger with two arguments can be fired.
    /// </summary>
    public Task<bool> CanFireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        return Task.FromResult(StateMachine.CanFire(tp, arg0, arg1));
    }

    /// <summary>
    /// Determines whether the specified trigger with two arguments can be fired and returns unmet guard conditions.
    /// </summary>
    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        var result = StateMachine.CanFire(tp, arg0, arg1, out var unmet);
        return Task.FromResult((result, unmet));
    }

    /// <summary>
    /// Determines whether the specified trigger with three arguments can be fired.
    /// </summary>
    public Task<bool> CanFireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        return Task.FromResult(StateMachine.CanFire(tp, arg0, arg1, arg2));
    }

    /// <summary>
    /// Determines whether the specified trigger with three arguments can be fired and returns unmet guard conditions.
    /// </summary>
    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        var result = StateMachine.CanFire(tp, arg0, arg1, arg2, out var unmet);
        return Task.FromResult((result, unmet));
    }

    /// <summary>
    /// Returns a string representation of the state machine.
    /// </summary>
    public Task<string> ToStringAsync()
    {
        return Task.FromResult(StateMachine.ToString());
    }

    /// <summary>
    /// Sets the correlation ID for tracking related events.
    /// </summary>
    public void SetCorrelationId(string correlationId)
    {
        _currentCorrelationId = correlationId;
    }

    /// <summary>
    /// Configures event sourcing options.
    /// </summary>
    protected virtual void ConfigureEventSourcing(EventSourcingOptions options)
    {
        // Override in derived classes to configure options
    }

    /// <summary>
    /// Builds the state machine instance.
    /// </summary>
    protected abstract StateMachine<TState, TTrigger> BuildStateMachine();

    /// <summary>
    /// Gets the version of the state machine definition.
    /// </summary>
    protected virtual string GetStateMachineVersion()
    {
        return "1.0.0";
    }

    /// <summary>
    /// Generates a deduplication key for idempotency.
    /// </summary>
    protected virtual string GenerateDedupeKey(TTrigger trigger, params object[] args)
    {
        var key = $"{this.GetPrimaryKeyString()}:{trigger}";
        if (args.Length > 0)
        {
            key += ":" + string.Join(":", args);
        }
        return key;
    }

    /// <summary>
    /// Checks if a trigger should be processed based on the dedupe key.
    /// </summary>
    protected virtual bool ShouldProcessTrigger(string? dedupeKey)
    {
        if (string.IsNullOrEmpty(dedupeKey) || !Options.EnableIdempotency)
        {
            return true;
        }

        if (_processedDedupeKeys.Contains(dedupeKey))
        {
            return false;
        }

        _processedDedupeKeys.Add(dedupeKey);
        return true;
    }

    /// <inheritdoc/>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        // Configure event sourcing options
        ConfigureEventSourcing(Options);

        // Build the state machine
        StateMachine = BuildStateMachine();
        NotNull(StateMachine, nameof(StateMachine));

        // Restore state from events
        if (State.CurrentState != null)
        {
            // Set the state machine to the persisted state
            var machine = new StateMachine<TState, TTrigger>(() => State.CurrentState, s => State.CurrentState = s);
            
            // Copy configuration from the built machine
            // Note: This is a simplified approach. In production, you'd want to properly restore the configuration
            StateMachine = BuildStateMachine();
        }

        // Replay events to rebuild dedupe key set
        await ReplayEventsForDeduplication();
    }

    /// <summary>
    /// Replays events to rebuild the deduplication key set.
    /// </summary>
    protected virtual async Task ReplayEventsForDeduplication()
    {
        // This would typically query the event store for recent events
        // For now, this is a placeholder
        await Task.CompletedTask;
    }

    /// <summary>
    /// Throws an exception if the provided object is null.
    /// </summary>
    private static void NotNull([NotNull] object? obj, string name)
    {
        if (obj == null)
            throw new InvalidOperationException($"{name} cannot be null");
    }
}

/// <summary>
/// State class for event-sourced state machines.
/// </summary>
[GenerateSerializer]
public class EventSourcedStateMachineState<TState>
{
    [Id(0)]
    public TState? CurrentState { get; set; }

    [Id(1)]
    public DateTime? LastTransitionTimestamp { get; set; }

    [Id(2)]
    public int TransitionCount { get; set; }

    [Id(3)]
    public string? StateMachineVersion { get; set; }
}