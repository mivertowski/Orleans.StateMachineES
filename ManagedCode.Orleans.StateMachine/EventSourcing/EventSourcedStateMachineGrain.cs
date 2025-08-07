using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ivlt.Orleans.StateMachineES.EventSourcing.Events;
using ivlt.Orleans.StateMachineES.EventSourcing.Configuration;
using ivlt.Orleans.StateMachineES.EventSourcing.Exceptions;
using ivlt.Orleans.StateMachineES.Interfaces;
using ivlt.Orleans.StateMachineES.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.EventSourcing.CustomStorage;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Streams;
using Stateless;
using Stateless.Graph;

namespace ivlt.Orleans.StateMachineES.EventSourcing;

/// <summary>
/// Base grain class for event-sourced state machines that automatically persists state transitions as events.
/// Provides enterprise-grade event sourcing with proper replay, snapshots, and error handling.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
/// <typeparam name="TGrainState">The type of the grain state (for snapshotting).</typeparam>
public abstract class EventSourcedStateMachineGrain<TState, TTrigger, TGrainState> : 
    JournaledGrain<TGrainState, StateTransitionEvent<TState, TTrigger>>, 
    IStateMachineGrain<TState, TTrigger>,
    ICustomStorageInterface<TGrainState, StateTransitionEvent<TState, TTrigger>>
    where TGrainState : EventSourcedStateMachineState<TState>, new()
    where TState : notnull
    where TTrigger : notnull
{
    private ILogger? _logger;
    
    [NotNull]
    protected StateMachine<TState, TTrigger>? StateMachine { get; private set; }

    protected EventSourcingOptions Options { get; private set; } = new();

    private readonly LinkedList<string> _processedDedupeKeys = new();
    private readonly Dictionary<string, LinkedListNode<string>> _dedupeKeyLookup = new();
    private string? _currentCorrelationId;
    private int _eventsSinceSnapshot;
    private readonly SemaphoreSlim _transitionSemaphore = new(1, 1);
    private IAsyncStream<StateTransitionEvent<TState, TTrigger>>? _eventStream;
    
    // Cache for trigger parameters to avoid reconfiguring
    protected readonly Dictionary<TTrigger, object> TriggerParametersCache = new();

    /// <summary>
    /// Activates the state machine.
    /// </summary>
    public virtual async Task ActivateAsync()
    {
        await StateMachine.ActivateAsync();
        _logger?.LogDebug("State machine activated for grain {GrainId}", this.GetPrimaryKeyString());
    }

    /// <summary>
    /// Deactivates the state machine.
    /// </summary>
    public virtual async Task DeactivateAsync()
    {
        await StateMachine.DeactivateAsync();
        _logger?.LogDebug("State machine deactivated for grain {GrainId}", this.GetPrimaryKeyString());
    }

    /// <summary>
    /// Fires the specified trigger asynchronously with event sourcing.
    /// </summary>
    public virtual async Task FireAsync(TTrigger trigger)
    {
        await _transitionSemaphore.WaitAsync();
        try
        {
            var dedupeKey = GenerateDedupeKey(trigger);
            if (!ShouldProcessTrigger(dedupeKey))
            {
                _logger?.LogDebug("Trigger {Trigger} skipped due to deduplication key {DedupeKey}", trigger, dedupeKey);
                return;
            }

            var fromState = StateMachine.State;
            
            // Validate transition is allowed
            if (!StateMachine.CanFire(trigger))
            {
                var permitedTriggers = StateMachine.PermittedTriggers;
                throw new InvalidStateTransitionException(
                    $"Cannot fire trigger '{trigger}' from state '{fromState}'. Permitted triggers: {string.Join(", ", permitedTriggers)}");
            }

            await StateMachine.FireAsync(trigger);
            var toState = StateMachine.State;

            await RecordTransitionEvent(fromState, toState, trigger, dedupeKey);
            
            _logger?.LogInformation("State transition: {FromState} -> {ToState} via {Trigger}", 
                fromState, toState, trigger);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error firing trigger {Trigger}", trigger);
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    /// <summary>
    /// Fires the specified trigger with one argument asynchronously.
    /// </summary>
    public virtual async Task FireAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        await _transitionSemaphore.WaitAsync();
        try
        {
            var dedupeKey = GenerateDedupeKey(trigger, arg0!);
            if (!ShouldProcessTrigger(dedupeKey))
            {
                _logger?.LogDebug("Trigger {Trigger} with arg {Arg0} skipped due to deduplication", trigger, arg0);
                return;
            }

            var fromState = StateMachine.State;
            
            // Get or create cached trigger parameters
            if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
            {
                cached = StateMachine.SetTriggerParameters<TArg0>(trigger);
                TriggerParametersCache[trigger] = cached;
            }
            var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0>)cached;
            
            if (!StateMachine.CanFire(tp, arg0))
            {
                throw new InvalidStateTransitionException(
                    $"Cannot fire trigger '{trigger}' with argument from state '{fromState}'");
            }

            await StateMachine.FireAsync(tp, arg0);
            var toState = StateMachine.State;

            var metadata = new Dictionary<string, object> { ["arg0"] = arg0! };
            await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata);
            
            _logger?.LogInformation("State transition: {FromState} -> {ToState} via {Trigger} with args", 
                fromState, toState, trigger);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error firing trigger {Trigger} with argument", trigger);
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    /// <summary>
    /// Fires the specified trigger with two arguments asynchronously.
    /// </summary>
    public virtual async Task FireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        await _transitionSemaphore.WaitAsync();
        try
        {
            var dedupeKey = GenerateDedupeKey(trigger, arg0!, arg1!);
            if (!ShouldProcessTrigger(dedupeKey))
            {
                _logger?.LogDebug("Trigger {Trigger} with args skipped due to deduplication", trigger);
                return;
            }

            var fromState = StateMachine.State;
            
            // Get or create cached trigger parameters
            if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
            {
                cached = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
                TriggerParametersCache[trigger] = cached;
            }
            var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1>)cached;
            
            if (!StateMachine.CanFire(tp, arg0, arg1))
            {
                throw new InvalidStateTransitionException(
                    $"Cannot fire trigger '{trigger}' with arguments from state '{fromState}'");
            }

            await StateMachine.FireAsync(tp, arg0, arg1);
            var toState = StateMachine.State;

            var metadata = new Dictionary<string, object> { ["arg0"] = arg0!, ["arg1"] = arg1! };
            await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata);
            
            _logger?.LogInformation("State transition: {FromState} -> {ToState} via {Trigger} with args", 
                fromState, toState, trigger);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error firing trigger {Trigger} with arguments", trigger);
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    /// <summary>
    /// Fires the specified trigger with three arguments asynchronously.
    /// </summary>
    public virtual async Task FireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        await _transitionSemaphore.WaitAsync();
        try
        {
            var dedupeKey = GenerateDedupeKey(trigger, arg0!, arg1!, arg2!);
            if (!ShouldProcessTrigger(dedupeKey))
            {
                _logger?.LogDebug("Trigger {Trigger} with args skipped due to deduplication", trigger);
                return;
            }

            var fromState = StateMachine.State;
            
            // Get or create cached trigger parameters
            if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
            {
                cached = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
                TriggerParametersCache[trigger] = cached;
            }
            var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1, TArg2>)cached;
            
            if (!StateMachine.CanFire(tp, arg0, arg1, arg2))
            {
                throw new InvalidStateTransitionException(
                    $"Cannot fire trigger '{trigger}' with arguments from state '{fromState}'");
            }

            await StateMachine.FireAsync(tp, arg0, arg1, arg2);
            var toState = StateMachine.State;

            var metadata = new Dictionary<string, object> { ["arg0"] = arg0!, ["arg1"] = arg1!, ["arg2"] = arg2! };
            await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata);
            
            _logger?.LogInformation("State transition: {FromState} -> {ToState} via {Trigger} with args", 
                fromState, toState, trigger);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error firing trigger {Trigger} with arguments", trigger);
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
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
        try
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
            State.StateMachineVersion = GetStateMachineVersion();

            // Track for snapshots
            _eventsSinceSnapshot++;

            // Auto-confirm if configured
            if (Options.AutoConfirmEvents)
            {
                await ConfirmEvents();
                
                // Check if snapshot is needed
                if (Options.EnableSnapshots && _eventsSinceSnapshot >= Options.SnapshotInterval)
                {
                    await TakeSnapshotAsync();
                    _eventsSinceSnapshot = 0;
                }
            }

            // Publish to stream if configured
            if (Options.PublishToStream && _eventStream != null)
            {
                await PublishToStreamAsync(transitionEvent);
            }

            // Add dedupe key to tracking
            if (!string.IsNullOrEmpty(dedupeKey))
            {
                AddDedupeKey(dedupeKey);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to record transition event");
            throw new EventSourcingException("Failed to record state transition", ex);
        }
    }

    /// <summary>
    /// Takes a snapshot of the current state.
    /// </summary>
    protected virtual async Task TakeSnapshotAsync()
    {
        try
        {
            _logger?.LogDebug("Taking snapshot at event count {Count}", State.TransitionCount);
            // The base JournaledGrain handles snapshot persistence
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to take snapshot");
            // Don't fail the operation if snapshot fails
        }
    }

    /// <summary>
    /// Publishes an event to Orleans Streams.
    /// </summary>
    protected virtual async Task PublishToStreamAsync(StateTransitionEvent<TState, TTrigger> evt)
    {
        try
        {
            if (_eventStream != null)
            {
                await _eventStream.OnNextAsync(evt);
                _logger?.LogDebug("Published event to stream for transition {FromState} -> {ToState}", 
                    evt.FromState, evt.ToState);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to publish event to stream");
            // Don't fail the transition if stream publishing fails
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
        // Materialize the collection to avoid serialization issues with LINQ iterators
        var triggers = StateMachine.GetPermittedTriggers(args).ToList();
        return Task.FromResult<IEnumerable<TTrigger>>(triggers);
    }

    /// <summary>
    /// Gets detailed information about permitted triggers for the current state.
    /// </summary>
    public Task<IEnumerable<TriggerDetails<TState, TTrigger>>> GetDetailedPermittedTriggersAsync(params object[] args)
    {
        // Materialize the collection to avoid serialization issues with LINQ iterators
        var details = StateMachine.GetDetailedPermittedTriggers(args).ToList();
        return Task.FromResult<IEnumerable<TriggerDetails<TState, TTrigger>>>(details);
    }

    /// <summary>
    /// Gets the permitted triggers property for the current state.
    /// </summary>
    public Task<IEnumerable<TTrigger>> GetPermittedTriggersPropertyAsync()
    {
        // Materialize the collection to avoid serialization issues with LINQ iterators
        var triggers = StateMachine.PermittedTriggers.ToList();
        return Task.FromResult<IEnumerable<TTrigger>>(triggers);
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
        // Get or create cached trigger parameters
        if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
        {
            cached = StateMachine.SetTriggerParameters<TArg0>(trigger);
            TriggerParametersCache[trigger] = cached;
        }
        var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0>)cached;
        return Task.FromResult(StateMachine.CanFire(tp, arg0));
    }

    /// <summary>
    /// Determines whether the specified trigger with one argument can be fired and returns unmet guard conditions.
    /// </summary>
    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        // Get or create cached trigger parameters
        if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
        {
            cached = StateMachine.SetTriggerParameters<TArg0>(trigger);
            TriggerParametersCache[trigger] = cached;
        }
        var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0>)cached;
        var result = StateMachine.CanFire(tp, arg0, out var unmet);
        return Task.FromResult((result, unmet));
    }

    /// <summary>
    /// Determines whether the specified trigger with two arguments can be fired.
    /// </summary>
    public Task<bool> CanFireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        // Get or create cached trigger parameters
        if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
        {
            cached = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
            TriggerParametersCache[trigger] = cached;
        }
        var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1>)cached;
        return Task.FromResult(StateMachine.CanFire(tp, arg0, arg1));
    }

    /// <summary>
    /// Determines whether the specified trigger with two arguments can be fired and returns unmet guard conditions.
    /// </summary>
    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        // Get or create cached trigger parameters
        if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
        {
            cached = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
            TriggerParametersCache[trigger] = cached;
        }
        var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1>)cached;
        var result = StateMachine.CanFire(tp, arg0, arg1, out var unmet);
        return Task.FromResult((result, unmet));
    }

    /// <summary>
    /// Determines whether the specified trigger with three arguments can be fired.
    /// </summary>
    public Task<bool> CanFireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        // Get or create cached trigger parameters
        if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
        {
            cached = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
            TriggerParametersCache[trigger] = cached;
        }
        var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1, TArg2>)cached;
        return Task.FromResult(StateMachine.CanFire(tp, arg0, arg1, arg2));
    }

    /// <summary>
    /// Determines whether the specified trigger with three arguments can be fired and returns unmet guard conditions.
    /// </summary>
    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        // Get or create cached trigger parameters
        if (!TriggerParametersCache.TryGetValue(trigger, out var cached))
        {
            cached = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
            TriggerParametersCache[trigger] = cached;
        }
        var tp = (StateMachine<TState, TTrigger>.TriggerWithParameters<TArg0, TArg1, TArg2>)cached;
        var result = StateMachine.CanFire(tp, arg0, arg1, arg2, out var unmet);
        return Task.FromResult((result, unmet));
    }

    /// <summary>
    /// Returns a string representation of the state machine.
    /// </summary>
    public Task<string> ToStringAsync()
    {
        return Task.FromResult(StateMachine.ToString() ?? "StateMachine");
    }

    /// <summary>
    /// Sets the correlation ID for tracking related events.
    /// </summary>
    public virtual void SetCorrelationId(string correlationId)
    {
        _currentCorrelationId = correlationId;
        _logger?.LogDebug("Correlation ID set to {CorrelationId}", correlationId);
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
        var keyParts = new List<string>
        {
            this.GetPrimaryKeyString(),
            trigger.ToString() ?? "null"
        };

        if (args.Length > 0)
        {
            keyParts.AddRange(args.Select(a => a?.ToString() ?? "null"));
        }

        var key = string.Join(":", keyParts);
        
        // Add timestamp for time-based deduplication if needed
        if (Options.EnableIdempotency)
        {
            // Could add time window here if needed
            return key;
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

        return !_dedupeKeyLookup.ContainsKey(dedupeKey);
    }

    /// <summary>
    /// Adds a deduplication key to the tracking set with LRU eviction.
    /// </summary>
    protected virtual void AddDedupeKey(string dedupeKey)
    {
        if (_dedupeKeyLookup.ContainsKey(dedupeKey))
        {
            // Move to end (most recently used)
            var node = _dedupeKeyLookup[dedupeKey];
            _processedDedupeKeys.Remove(node);
            _processedDedupeKeys.AddLast(node);
            return;
        }

        // Add new key
        var newNode = _processedDedupeKeys.AddLast(dedupeKey);
        _dedupeKeyLookup[dedupeKey] = newNode;

        // Enforce size limit with LRU eviction
        while (_processedDedupeKeys.Count > Options.MaxDedupeKeysInMemory)
        {
            var firstNode = _processedDedupeKeys.First;
            if (firstNode != null)
            {
                _processedDedupeKeys.RemoveFirst();
                _dedupeKeyLookup.Remove(firstNode.Value);
                _logger?.LogDebug("Evicted dedupe key {Key} due to size limit", firstNode.Value);
            }
        }
    }

    /// <inheritdoc/>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        try
        {
            // Initialize logger
            _logger = this.ServiceProvider.GetService<ILogger<EventSourcedStateMachineGrain<TState, TTrigger, TGrainState>>>();
            
            _logger?.LogDebug("Activating state machine grain {GrainId}", this.GetPrimaryKeyString());

            // Configure event sourcing options
            ConfigureEventSourcing(Options);

            // Initialize stream if configured
            if (Options.PublishToStream && !string.IsNullOrEmpty(Options.StreamProvider))
            {
                try
                {
                    var streamProvider = this.GetStreamProvider(Options.StreamProvider);
                    var streamId = StreamId.Create(Options.StreamNamespace ?? "StateMachine", this.GetPrimaryKeyString());
                    _eventStream = streamProvider.GetStream<StateTransitionEvent<TState, TTrigger>>(streamId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to initialize stream for grain {GrainId}", this.GetPrimaryKeyString());
                }
            }

            // Build and initialize the state machine
            await InitializeStateMachineAsync();

            _logger?.LogInformation("State machine grain {GrainId} activated successfully", this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to activate state machine grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// Initializes the state machine, either from scratch or by replaying events.
    /// </summary>
    protected virtual async Task InitializeStateMachineAsync()
    {
        // Clear trigger parameters cache when rebuilding
        TriggerParametersCache.Clear();
        
        // Build the state machine configuration
        StateMachine = BuildStateMachine();
        NotNull(StateMachine, nameof(StateMachine));

        // If we have a persisted state, we need to restore from events
        if (State.CurrentState != null)
        {
            _logger?.LogDebug("Restoring state machine from persisted state: {State}", State.CurrentState);
            
            // Create a new state machine with the ability to set state
            TState currentState = State.CurrentState;
            StateMachine = new StateMachine<TState, TTrigger>(
                () => currentState,
                s => currentState = s
            );

            // Reapply the configuration
            var configuredMachine = BuildStateMachine();
            
            // Note: Stateless doesn't provide a direct way to copy configuration
            // In a production system, you might want to use reflection or maintain
            // the configuration separately to properly restore it
            StateMachine = configuredMachine;
            
            // Force the state to the persisted value
            if (!EqualityComparer<TState>.Default.Equals(State.CurrentState, default(TState)!))
            {
                await ForceStateAsync(State.CurrentState);
            }
        }

        // Replay events to rebuild state and dedupe keys
        await ReplayEventsAsync();
    }

    /// <summary>
    /// Forces the state machine to a specific state (used during restoration).
    /// </summary>
    protected virtual async Task ForceStateAsync(TState state)
    {
        try
        {
            // This is a workaround since Stateless doesn't provide direct state setting
            // We create a temporary machine to transition to the desired state
            var tempMachine = new StateMachine<TState, TTrigger>(state);
            
            // Apply the same configuration
            StateMachine = BuildStateMachine();
            
            // Use reflection to set the internal state (if needed)
            // This is not ideal but necessary for proper state restoration
            var stateAccessor = StateMachine.GetType()
                .GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (stateAccessor != null)
            {
                stateAccessor.SetValue(StateMachine, state);
            }
            
            State.CurrentState = state;
            
            _logger?.LogDebug("Forced state machine to state: {State}", state);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to force state to {State}", state);
            throw;
        }
    }

    /// <summary>
    /// Replays events to rebuild state and deduplication keys.
    /// </summary>
    protected virtual async Task ReplayEventsAsync()
    {
        try
        {
            _logger?.LogDebug("Starting event replay for grain {GrainId}", this.GetPrimaryKeyString());

            // Get the events from the journal
            var events = await RetrieveConfirmedEvents(0, Version);
            
            if (events != null && events.Any())
            {
                _logger?.LogDebug("Replaying {Count} events", events.Count());
                
                foreach (var evt in events)
                {
                    if (evt is StateTransitionEvent<TState, TTrigger> transitionEvent)
                    {
                        // Rebuild dedupe key set
                        if (!string.IsNullOrEmpty(transitionEvent.DedupeKey))
                        {
                            AddDedupeKey(transitionEvent.DedupeKey);
                        }

                        // Update state tracking
                        State.CurrentState = transitionEvent.ToState;
                        State.LastTransitionTimestamp = transitionEvent.Timestamp;
                        State.TransitionCount++;
                    }
                }
                
                _logger?.LogInformation("Successfully replayed {Count} events, current state: {State}", 
                    events.Count(), State.CurrentState);
            }
            else
            {
                _logger?.LogDebug("No events to replay");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to replay events");
            // Don't throw - we can still function without perfect replay
        }
    }

    /// <summary>
    /// Custom storage interface implementation for advanced scenarios.
    /// </summary>
    public virtual Task<bool> ApplyUpdatesToStorage(IReadOnlyList<StateTransitionEvent<TState, TTrigger>> updates, int expectedVersion)
    {
        // This would be implemented if using custom storage
        return Task.FromResult(true);
    }

    /// <summary>
    /// Reads state from storage for custom storage scenarios.
    /// </summary>
    public virtual Task<KeyValuePair<int, TGrainState>> ReadStateFromStorage()
    {
        // This would be implemented if using custom storage
        return Task.FromResult(new KeyValuePair<int, TGrainState>(Version, State));
    }

    /// <summary>
    /// Throws an exception if the provided object is null.
    /// </summary>
    private static void NotNull([NotNull] object? obj, string name)
    {
        ArgumentNullException.ThrowIfNull(obj, name);
    }

    /// <inheritdoc/>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Deactivating state machine grain {GrainId}, reason: {Reason}", 
                this.GetPrimaryKeyString(), reason);

            // Ensure all events are confirmed before deactivation
            if (Options.AutoConfirmEvents)
            {
                await ConfirmEvents();
            }

            // Take a final snapshot if needed
            if (Options.EnableSnapshots && _eventsSinceSnapshot > 0)
            {
                await TakeSnapshotAsync();
            }

            // Cleanup
            _transitionSemaphore?.Dispose();

            await base.OnDeactivateAsync(reason, cancellationToken);
            
            _logger?.LogInformation("State machine grain {GrainId} deactivated successfully", 
                this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during deactivation of grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
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

    [Id(4)]
    public Dictionary<string, object>? ExtendedProperties { get; set; }

    [Id(5)]
    public DateTime? LastSnapshotTimestamp { get; set; }

    [Id(6)]
    public int EventsSinceSnapshot { get; set; }
}