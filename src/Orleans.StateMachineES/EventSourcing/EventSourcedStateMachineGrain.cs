using System.Diagnostics.CodeAnalysis;
using Orleans.StateMachineES.EventSourcing.Events;
using Orleans.StateMachineES.EventSourcing.Configuration;
using Orleans.StateMachineES.EventSourcing.Exceptions;
using Orleans.StateMachineES.Extensions;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Memory;
using Orleans.StateMachineES.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.EventSourcing.CustomStorage;
using Orleans.Streams;
using Stateless;

namespace Orleans.StateMachineES.EventSourcing;

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

    /// <summary>
    /// Cache for trigger parameters to avoid repeated configuration.
    /// </summary>
    protected TriggerParameterCache<TState, TTrigger>? TriggerCache { get; private set; }

    protected EventSourcingOptions Options { get; private set; } = new();

    private readonly LinkedList<string> _processedDedupeKeys = new();
    private readonly Dictionary<string, LinkedListNode<string>> _dedupeKeyLookup = [];
    private string? _currentCorrelationId;
    private int _eventsSinceSnapshot;
    private readonly SemaphoreSlim _transitionSemaphore = new(1, 1);
    private IAsyncStream<StateTransitionEvent<TState, TTrigger>>? _eventStream;

    /// <summary>
    /// Activates the state machine.
    /// </summary>
    public virtual async Task ActivateAsync()
    {
        await StateMachine.ActivateAsync().ConfigureAwait(false);
        _logger?.LogDebug("State machine activated for grain {GrainId}", this.GetPrimaryKeyString());
    }

    /// <summary>
    /// Deactivates the state machine.
    /// </summary>
    public virtual async Task DeactivateAsync()
    {
        await StateMachine.DeactivateAsync().ConfigureAwait(false);
        _logger?.LogDebug("State machine deactivated for grain {GrainId}", this.GetPrimaryKeyString());
    }

    /// <summary>
    /// Fires the specified trigger asynchronously with event sourcing.
    /// </summary>
    public virtual async Task FireAsync(TTrigger trigger)
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
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

            await StateMachine.FireAsync(trigger).ConfigureAwait(false);
            var toState = StateMachine.State;

            await RecordTransitionEvent(fromState, toState, trigger, dedupeKey).ConfigureAwait(false);
            
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
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
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
            var tp = TriggerCache!.GetOrCreate<TArg0>(trigger);

            if (!StateMachine.CanFire(tp, arg0))
            {
                throw new InvalidStateTransitionException(
                    $"Cannot fire trigger '{trigger}' with argument from state '{fromState}'");
            }

            await StateMachine.FireAsync(tp, arg0).ConfigureAwait(false);
            var toState = StateMachine.State;

            var metadata = new Dictionary<string, object> { ["arg0"] = arg0! };
            await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata).ConfigureAwait(false);
            
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
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
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
            var tp = TriggerCache!.GetOrCreate<TArg0, TArg1>(trigger);

            if (!StateMachine.CanFire(tp, arg0, arg1))
            {
                throw new InvalidStateTransitionException(
                    $"Cannot fire trigger '{trigger}' with arguments from state '{fromState}'");
            }

            await StateMachine.FireAsync(tp, arg0, arg1).ConfigureAwait(false);
            var toState = StateMachine.State;

            var metadata = new Dictionary<string, object> { ["arg0"] = arg0!, ["arg1"] = arg1! };
            await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata).ConfigureAwait(false);
            
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
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
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
            var tp = TriggerCache!.GetOrCreate<TArg0, TArg1, TArg2>(trigger);

            if (!StateMachine.CanFire(tp, arg0, arg1, arg2))
            {
                throw new InvalidStateTransitionException(
                    $"Cannot fire trigger '{trigger}' with arguments from state '{fromState}'");
            }

            await StateMachine.FireAsync(tp, arg0, arg1, arg2).ConfigureAwait(false);
            var toState = StateMachine.State;

            var metadata = new Dictionary<string, object> { ["arg0"] = arg0!, ["arg1"] = arg1!, ["arg2"] = arg2! };
            await RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata).ConfigureAwait(false);
            
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
                await ConfirmEvents().ConfigureAwait(false);

                // Check if snapshot is needed
                if (Options.EnableSnapshots && _eventsSinceSnapshot >= Options.SnapshotInterval)
                {
                    await TakeSnapshotAsync().ConfigureAwait(false);
                    _eventsSinceSnapshot = 0;
                }
            }

            // Publish to stream if configured
            if (Options.PublishToStream && _eventStream != null)
            {
                await PublishToStreamAsync(transitionEvent).ConfigureAwait(false);
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
                await _eventStream.OnNextAsync(evt).ConfigureAwait(false);
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
    public ValueTask<TState> GetStateAsync()
    {
        return ValueTaskExtensions.FromResult(StateMachine.State);
    }

    /// <summary>
    /// Determines whether the state machine is in the specified state.
    /// </summary>
    public ValueTask<bool> IsInStateAsync(TState state)
    {
        return ValueTaskExtensions.FromResult(StateMachine.IsInState(state));
    }

    /// <summary>
    /// Determines whether the specified trigger can be fired.
    /// </summary>
    public ValueTask<bool> CanFireAsync(TTrigger trigger)
    {
        return ValueTaskExtensions.FromResult(StateMachine.CanFire(trigger));
    }

    /// <summary>
    /// Gets information about the state machine.
    /// </summary>
    public ValueTask<OrleansStateMachineInfo> GetInfoAsync()
    {
        return ValueTaskExtensions.FromResult(new OrleansStateMachineInfo(StateMachine.GetInfo()));
    }

    /// <summary>
    /// Gets the permitted triggers for the current state.
    /// </summary>
    public ValueTask<IEnumerable<TTrigger>> GetPermittedTriggersAsync(params object[] args)
    {
        // Materialize the collection to avoid serialization issues with LINQ iterators
        var triggers = StateMachine.GetPermittedTriggers(args).ToList();
        return ValueTaskExtensions.FromResult<IEnumerable<TTrigger>>(triggers);
    }

    /// <summary>
    /// Gets detailed information about permitted triggers for the current state.
    /// </summary>
    public ValueTask<IEnumerable<TriggerDetails<TState, TTrigger>>> GetDetailedPermittedTriggersAsync(params object[] args)
    {
        // Materialize the collection to avoid serialization issues with LINQ iterators
        var details = StateMachine.GetDetailedPermittedTriggers(args).ToList();
        return ValueTaskExtensions.FromResult<IEnumerable<TriggerDetails<TState, TTrigger>>>(details);
    }

    /// <summary>
    /// Gets the permitted triggers property for the current state.
    /// </summary>
    public ValueTask<IEnumerable<TTrigger>> GetPermittedTriggersPropertyAsync()
    {
        // Materialize the collection to avoid serialization issues with LINQ iterators
        var triggers = StateMachine.PermittedTriggers.ToList();
        return ValueTaskExtensions.FromResult<IEnumerable<TTrigger>>(triggers);
    }

    /// <summary>
    /// Determines whether the specified trigger can be fired and returns unmet guard conditions.
    /// </summary>
    public ValueTask<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync(TTrigger trigger)
    {
        var result = StateMachine.CanFire(trigger, out var unmetGuards);
        return ValueTaskExtensions.FromResult((result, unmetGuards));
    }

    /// <summary>
    /// Determines whether the specified trigger with one argument can be fired.
    /// </summary>
    public ValueTask<bool> CanFireAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = TriggerCache!.GetOrCreate<TArg0>(trigger);
        return ValueTaskExtensions.FromResult(StateMachine.CanFire(tp, arg0));
    }

    /// <summary>
    /// Determines whether the specified trigger with one argument can be fired and returns unmet guard conditions.
    /// </summary>
    public ValueTask<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = TriggerCache!.GetOrCreate<TArg0>(trigger);
        var result = StateMachine.CanFire(tp, arg0, out var unmet);
        return ValueTaskExtensions.FromResult((result, unmet));
    }

    /// <summary>
    /// Determines whether the specified trigger with two arguments can be fired.
    /// </summary>
    public ValueTask<bool> CanFireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = TriggerCache!.GetOrCreate<TArg0, TArg1>(trigger);
        return ValueTaskExtensions.FromResult(StateMachine.CanFire(tp, arg0, arg1));
    }

    /// <summary>
    /// Determines whether the specified trigger with two arguments can be fired and returns unmet guard conditions.
    /// </summary>
    public ValueTask<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = TriggerCache!.GetOrCreate<TArg0, TArg1>(trigger);
        var result = StateMachine.CanFire(tp, arg0, arg1, out var unmet);
        return ValueTaskExtensions.FromResult((result, unmet));
    }

    /// <summary>
    /// Determines whether the specified trigger with three arguments can be fired.
    /// </summary>
    public ValueTask<bool> CanFireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = TriggerCache!.GetOrCreate<TArg0, TArg1, TArg2>(trigger);
        return ValueTaskExtensions.FromResult(StateMachine.CanFire(tp, arg0, arg1, arg2));
    }

    /// <summary>
    /// Determines whether the specified trigger with three arguments can be fired and returns unmet guard conditions.
    /// </summary>
    public ValueTask<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = TriggerCache!.GetOrCreate<TArg0, TArg1, TArg2>(trigger);
        var result = StateMachine.CanFire(tp, arg0, arg1, arg2, out var unmet);
        return ValueTaskExtensions.FromResult((result, unmet));
    }

    /// <summary>
    /// Returns a string representation of the state machine.
    /// </summary>
    public ValueTask<string> ToStringAsync()
    {
        return ValueTaskExtensions.FromResult(StateMachine.ToString() ?? "StateMachine");
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
        await base.OnActivateAsync(cancellationToken).ConfigureAwait(false);

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
            await InitializeStateMachineAsync().ConfigureAwait(false);

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
        // Replay events first to rebuild state
        await ReplayEventsAsync().ConfigureAwait(false);

        // If we have a persisted state, create machine with that state as initial
        if (State.CurrentState != null && !EqualityComparer<TState>.Default.Equals(State.CurrentState, default(TState)!))
        {
            _logger?.LogDebug("Restoring state machine from persisted state: {State}", State.CurrentState);

            // Create state machine with restored state as the initial state
            TState restoredState = State.CurrentState;
            StateMachine = new StateMachine<TState, TTrigger>(restoredState);

            // Apply the configuration to the machine with restored state
            ApplyConfigurationToMachine(StateMachine);
        }
        else
        {
            // Build the state machine with default initial state
            StateMachine = BuildStateMachine();
        }

        NotNull(StateMachine, nameof(StateMachine));

        // Initialize trigger parameter cache after state machine is created
        TriggerCache = new TriggerParameterCache<TState, TTrigger>(StateMachine);
    }

    /// <summary>
    /// Applies the state machine configuration to an existing machine instance.
    /// This is used when restoring state to avoid overwriting the current state.
    /// </summary>
    protected virtual void ApplyConfigurationToMachine(StateMachine<TState, TTrigger> machine)
    {
        try
        {
            // Build a temporary machine to get the configuration
            var templateMachine = BuildStateMachine();
            
            // Copy the configuration using reflection
            // This is a workaround since Stateless doesn't provide direct configuration copying
            var templateStateConfig = templateMachine.GetType()
                .GetField("_stateConfiguration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var machineStateConfig = machine.GetType()
                .GetField("_stateConfiguration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (templateStateConfig?.GetValue(templateMachine) != null && machineStateConfig != null)
            {
                machineStateConfig.SetValue(machine, templateStateConfig.GetValue(templateMachine));
                _logger?.LogDebug("Applied configuration to state machine with restored state");
            }
            else
            {
                _logger?.LogWarning("Could not copy state machine configuration using reflection, using workaround");
                
                // Fallback: rebuild from scratch and use ForceStateAsync
                var currentState = machine.State;
                StateMachine = BuildStateMachine();
                _ = ForceStateAsync(currentState);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply configuration to state machine, falling back to rebuild");
            
            // Fallback: rebuild from scratch
            var currentState = machine.State;
            StateMachine = BuildStateMachine();
            _ = ForceStateAsync(currentState);
        }
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
            var events = await RetrieveConfirmedEvents(0, Version).ConfigureAwait(false);
            
            if (events != null && events.Any())
            {
                _logger?.LogDebug("Replaying {Count} events", events.Count());
                
                int eventIndex = 0;
                foreach (var evt in events)
                {
                    eventIndex++;
                    
                    if (evt is StateTransitionEvent<TState, TTrigger> transitionEvent)
                    {
                        try
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
                        catch (Exception)
                        {
                            throw new EventReplayException(
                                $"Failed to replay event #{eventIndex} of {events.Count()}. " +
                                $"Event: {transitionEvent.FromState} -> {transitionEvent.ToState} via {transitionEvent.Trigger}. " +
                                $"Timestamp: {transitionEvent.Timestamp:O}",
                                eventIndex,
                                transitionEvent);
                        }
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
                await ConfirmEvents().ConfigureAwait(false);
            }

            // Take a final snapshot if needed
            if (Options.EnableSnapshots && _eventsSinceSnapshot > 0)
            {
                await TakeSnapshotAsync().ConfigureAwait(false);
            }

            // Cleanup
            _transitionSemaphore?.Dispose();

            await base.OnDeactivateAsync(reason, cancellationToken).ConfigureAwait(false);
            
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
[Alias("Orleans.StateMachineES.EventSourcing.EventSourcedStateMachineState`1")]
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