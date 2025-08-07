using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans.StateMachineES.EventSourcing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Runtime;
using Orleans.Timers;
using Stateless;

namespace Orleans.StateMachineES.Timers;

/// <summary>
/// Base class for state machines with timer and reminder support.
/// Extends EventSourcedStateMachineGrain to add time-based state transitions.
/// </summary>
public abstract class TimerEnabledStateMachineGrain<TState, TTrigger, TGrainState> :
    EventSourcedStateMachineGrain<TState, TTrigger, TGrainState>,
    IRemindable
    where TGrainState : TimerEnabledStateMachineState<TState>, new()
    where TState : notnull
    where TTrigger : notnull
{
    private ILogger<TimerEnabledStateMachineGrain<TState, TTrigger, TGrainState>>? _timerLogger;
    private readonly Dictionary<string, IGrainTimer> _activeTimers = new();
    private readonly Dictionary<string, IGrainReminder> _activeReminders = new();
    private readonly Dictionary<TState, List<TimerConfiguration>> _stateTimeouts = new();
    private IReminderRegistry? _reminderRegistry;

    /// <summary>
    /// Configures a timeout for a specific state.
    /// </summary>
    protected TimeoutBuilder<TState, TTrigger> ConfigureTimeout(TState state)
    {
        return new TimeoutBuilder<TState, TTrigger>(state);
    }

    /// <summary>
    /// Registers a timeout configuration for a state.
    /// </summary>
    protected void RegisterStateTimeout(TState state, TimerConfiguration config)
    {
        if (!_stateTimeouts.ContainsKey(state))
        {
            _stateTimeouts[state] = new List<TimerConfiguration>();
        }
        _stateTimeouts[state].Add(config);
        
        _timerLogger?.LogDebug("Registered timeout for state {State}: {TimeoutName} after {Timeout}", 
            state, config.Name, config.Timeout);
    }

    /// <summary>
    /// Configures state timeouts. Override in derived classes.
    /// </summary>
    protected virtual void ConfigureTimeouts()
    {
        // Override in derived classes to configure state timeouts
        // Example:
        // RegisterStateTimeout(States.Processing, 
        //     ConfigureTimeout(States.Processing)
        //         .After(TimeSpan.FromHours(1))
        //         .TransitionTo(Triggers.Timeout)
        //         .Build());
    }

    /// <inheritdoc/>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        
        _timerLogger = this.ServiceProvider.GetService<ILogger<TimerEnabledStateMachineGrain<TState, TTrigger, TGrainState>>>();
        _reminderRegistry = this.ServiceProvider.GetRequiredService<IReminderRegistry>();
        
        // Configure timeouts
        ConfigureTimeouts();
        
        // Hook into state machine transitions to manage timers
        // Note: StateMachine.OnTransitioned doesn't work with EventSourcedStateMachineGrain 
        // because it uses its own FireAsync implementation. We override RecordTransitionEvent instead.
        
        // Restore active timers/reminders from state
        await RestoreActiveTimersAsync();
        
        _timerLogger?.LogInformation("Timer-enabled state machine grain {GrainId} activated", this.GetPrimaryKeyString());
    }


    /// <summary>
    /// Starts all timers configured for a state.
    /// </summary>
    private async Task StartStateTimersAsync(TState state)
    {
        _timerLogger?.LogInformation("StartStateTimersAsync called for state {State}", state);
        
        if (!_stateTimeouts.TryGetValue(state, out var configs))
        {
            _timerLogger?.LogInformation("No timeout configurations found for state {State}", state);
            return;
        }
        
        _timerLogger?.LogInformation("Found {Count} timeout configurations for state {State}", configs.Count, state);

        foreach (var config in configs)
        {
            try
            {
                if (config.UseDurableReminder)
                {
                    await StartReminderAsync(config);
                }
                else
                {
                    StartTimer(config);
                }
                
                // Save to state for recovery
                State.ActiveTimerConfigs ??= new List<TimerConfiguration>();
                State.ActiveTimerConfigs.Add(config);
            }
            catch (Exception ex)
            {
                _timerLogger?.LogError(ex, "Failed to start timer {TimerName}", config.Name);
            }
        }
    }

    /// <summary>
    /// Cancels all timers for a state.
    /// </summary>
    private async Task CancelStateTimersAsync(TState state)
    {
        if (!_stateTimeouts.TryGetValue(state, out var configs))
        {
            return;
        }

        foreach (var config in configs)
        {
            try
            {
                if (config.UseDurableReminder)
                {
                    await CancelReminderAsync(config.Name);
                }
                else
                {
                    CancelTimer(config.Name);
                }
                
                // Remove from state
                State.ActiveTimerConfigs?.RemoveAll(c => c.Name == config.Name);
            }
            catch (Exception ex)
            {
                _timerLogger?.LogError(ex, "Failed to cancel timer {TimerName}", config.Name);
            }
        }
    }

    /// <summary>
    /// Starts a non-durable timer.
    /// </summary>
    private void StartTimer(TimerConfiguration config)
    {
        if (_activeTimers.ContainsKey(config.Name))
        {
            _timerLogger?.LogDebug("Timer {TimerName} already active, skipping", config.Name);
            return;
        }

        try
        {
            // Use the extension method directly on the grain with the correct signature
            var timer = this.RegisterGrainTimer<TimerConfiguration>(
                async (state, cancellationToken) => 
                {
                    await OnTimerTickAsync(state);
                },
                config,
                new GrainTimerCreationOptions
                {
                    DueTime = config.Timeout,
                    Period = config.IsRepeating ? config.Timeout : Timeout.InfiniteTimeSpan,
                    Interleave = true
                });

            _activeTimers[config.Name] = timer;
            _timerLogger?.LogInformation("Started timer {TimerName} with timeout {Timeout}, period {Period}, timer is not null: {TimerNotNull}", 
                config.Name, config.Timeout, config.IsRepeating ? config.Timeout : Timeout.InfiniteTimeSpan, timer != null);
        }
        catch (Exception ex)
        {
            _timerLogger?.LogError(ex, "Failed to register timer {TimerName}", config.Name);
        }
    }

    /// <summary>
    /// Cancels a non-durable timer.
    /// </summary>
    private void CancelTimer(string timerName)
    {
        if (_activeTimers.TryGetValue(timerName, out var timer))
        {
            timer.Dispose();
            _activeTimers.Remove(timerName);
            _timerLogger?.LogDebug("Cancelled timer {TimerName}", timerName);
        }
    }

    /// <summary>
    /// Starts a durable reminder.
    /// </summary>
    private async Task StartReminderAsync(TimerConfiguration config)
    {
        if (_activeReminders.ContainsKey(config.Name))
        {
            _timerLogger?.LogDebug("Reminder {ReminderName} already active, skipping", config.Name);
            return;
        }

        var reminder = await _reminderRegistry!.RegisterOrUpdateReminder(
            callingGrainId: this.GetGrainId(),
            reminderName: config.Name,
            dueTime: config.Timeout,
            period: config.IsRepeating ? config.Timeout : Timeout.InfiniteTimeSpan);

        _activeReminders[config.Name] = reminder;
        _timerLogger?.LogDebug("Started reminder {ReminderName} with timeout {Timeout}", config.Name, config.Timeout);
    }

    /// <summary>
    /// Cancels a durable reminder.
    /// </summary>
    private async Task CancelReminderAsync(string reminderName)
    {
        if (_activeReminders.TryGetValue(reminderName, out var reminder))
        {
            await _reminderRegistry!.UnregisterReminder(this.GetGrainId(), reminder);
            _activeReminders.Remove(reminderName);
            _timerLogger?.LogDebug("Cancelled reminder {ReminderName}", reminderName);
        }
    }

    /// <summary>
    /// Handles timer tick events.
    /// </summary>
    private async Task OnTimerTickAsync(TimerConfiguration config)
    {
        try
        {
            _timerLogger?.LogInformation("Timer {TimerName} fired for state {State}, trigger {Trigger}", 
                config.Name, config.State, config.TimeoutTrigger);
            
            if (config.TimeoutTrigger is TTrigger trigger)
            {
                // Check if we're still in the state this timer was configured for
                if (EqualityComparer<TState>.Default.Equals(StateMachine.State, (TState)config.State!))
                {
                    _timerLogger?.LogInformation("Firing timeout trigger {Trigger} for state {State}", 
                        trigger, config.State);
                    await FireAsync(trigger);
                    _timerLogger?.LogInformation("Successfully fired timeout trigger {Trigger}", trigger);
                }
                else
                {
                    _timerLogger?.LogWarning("Timer {TimerName} fired but state has changed from {ExpectedState} to {CurrentState}", 
                        config.Name, config.State, StateMachine.State);
                }
            }

            // Cancel non-repeating timers
            if (!config.IsRepeating && !config.UseDurableReminder)
            {
                CancelTimer(config.Name);
            }
        }
        catch (Exception ex)
        {
            _timerLogger?.LogError(ex, "Error handling timer tick for {TimerName}", config.Name);
            await OnTimerErrorAsync(config, ex);
        }
    }

    /// <summary>
    /// Handles reminder tick events.
    /// </summary>
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        try
        {
            _timerLogger?.LogDebug("Reminder {ReminderName} fired", reminderName);
            
            // Find the configuration for this reminder
            var config = State.ActiveTimerConfigs?.FirstOrDefault(c => c.Name == reminderName);
            if (config != null)
            {
                await OnTimerTickAsync(config);
                
                // Cancel non-repeating reminders
                if (!config.IsRepeating)
                {
                    await CancelReminderAsync(reminderName);
                }
            }
            else
            {
                _timerLogger?.LogWarning("Received reminder {ReminderName} but no configuration found", reminderName);
            }
        }
        catch (Exception ex)
        {
            _timerLogger?.LogError(ex, "Error handling reminder {ReminderName}", reminderName);
        }
    }

    /// <summary>
    /// Restores active timers/reminders after grain activation.
    /// </summary>
    private async Task RestoreActiveTimersAsync()
    {
        if (State.ActiveTimerConfigs == null || State.ActiveTimerConfigs.Count == 0)
        {
            return;
        }

        _timerLogger?.LogDebug("Restoring {Count} active timers", State.ActiveTimerConfigs.Count);

        foreach (var config in State.ActiveTimerConfigs.ToList())
        {
            try
            {
                // Check if the timer is still valid for current state
                if (EqualityComparer<TState>.Default.Equals(StateMachine.State, (TState)config.State!))
                {
                    if (config.UseDurableReminder)
                    {
                        // Reminders are automatically restored by Orleans
                        var reminder = await _reminderRegistry!.GetReminder(this.GetGrainId(), config.Name);
                        if (reminder != null)
                        {
                            _activeReminders[config.Name] = reminder;
                        }
                    }
                    else
                    {
                        // Restart non-durable timers
                        StartTimer(config);
                    }
                }
                else
                {
                    // Remove timers for different states
                    State.ActiveTimerConfigs.Remove(config);
                }
            }
            catch (Exception ex)
            {
                _timerLogger?.LogError(ex, "Failed to restore timer {TimerName}", config.Name);
            }
        }
    }

    /// <summary>
    /// Called when a timer error occurs. Override to implement custom error handling.
    /// </summary>
    protected virtual Task OnTimerErrorAsync(TimerConfiguration config, Exception exception)
    {
        // Default: log and continue
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override to manage timers when state transitions are recorded.
    /// </summary>
    protected override async Task RecordTransitionEvent(TState fromState, TState toState, TTrigger trigger, string? dedupeKey, Dictionary<string, object>? metadata = null)
    {
        // Call base implementation first to record the event
        await base.RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata);

        // Now manage timers for the state transition
        try
        {
            _timerLogger?.LogInformation("Managing timers for state transition: {FromState} -> {ToState} via {Trigger}", 
                fromState, toState, trigger);

            // Cancel timers for the previous state
            await CancelStateTimersAsync(fromState);
            
            // Start timers for the new state
            await StartStateTimersAsync(toState);
            
            _timerLogger?.LogInformation("Successfully managed timers for transition from {FromState} to {ToState}", 
                fromState, toState);
        }
        catch (Exception ex)
        {
            _timerLogger?.LogError(ex, "Error managing timers during state transition from {FromState} to {ToState}", 
                fromState, toState);
            // Don't fail the entire transition if timer management fails
        }
    }

    /// <summary>
    /// Cancels all active timers and reminders.
    /// </summary>
    public async Task CancelAllTimersAsync()
    {
        _timerLogger?.LogDebug("Cancelling all timers and reminders");

        // Cancel all timers
        foreach (var timer in _activeTimers.Values)
        {
            timer.Dispose();
        }
        _activeTimers.Clear();

        // Cancel all reminders
        foreach (var reminder in _activeReminders.Values)
        {
            await _reminderRegistry!.UnregisterReminder(this.GetGrainId(), reminder);
        }
        _activeReminders.Clear();

        // Clear state
        State.ActiveTimerConfigs?.Clear();
    }

    /// <inheritdoc/>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        // Cancel all non-durable timers (reminders persist)
        foreach (var timer in _activeTimers.Values)
        {
            timer.Dispose();
        }
        _activeTimers.Clear();

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}

/// <summary>
/// State class for timer-enabled state machines.
/// </summary>
[GenerateSerializer]
public class TimerEnabledStateMachineState<TState> : EventSourcedStateMachineState<TState>
{
    /// <summary>
    /// Gets or sets the list of active timer configurations.
    /// </summary>
    [Id(7)]
    public List<TimerConfiguration>? ActiveTimerConfigs { get; set; }
}