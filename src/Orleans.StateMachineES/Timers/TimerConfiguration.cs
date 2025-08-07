using System;
using System.Collections.Generic;
using Orleans;

namespace Orleans.StateMachineES.Timers;

/// <summary>
/// Configuration for state machine timers and reminders.
/// </summary>
[GenerateSerializer]
public class TimerConfiguration
{
    /// <summary>
    /// Gets or sets the timer/reminder name.
    /// </summary>
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timeout duration.
    /// </summary>
    [Id(1)]
    public TimeSpan Timeout { get; set; }

    /// <summary>
    /// Gets or sets the trigger to fire when the timeout occurs.
    /// </summary>
    [Id(2)]
    public object? TimeoutTrigger { get; set; }

    /// <summary>
    /// Gets or sets whether to use a durable reminder (true) or a timer (false).
    /// </summary>
    [Id(3)]
    public bool UseDurableReminder { get; set; }

    /// <summary>
    /// Gets or sets whether the timer should repeat.
    /// </summary>
    [Id(4)]
    public bool IsRepeating { get; set; }

    /// <summary>
    /// Gets or sets the state this timer is associated with.
    /// </summary>
    [Id(5)]
    public object? State { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for the timer.
    /// </summary>
    [Id(6)]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Fluent builder for configuring state timeouts.
/// </summary>
public class TimeoutBuilder<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly TimerConfiguration _config = new();
    private readonly TState _state;

    public TimeoutBuilder(TState state)
    {
        _state = state;
        _config.State = state;
    }

    /// <summary>
    /// Sets the timeout duration.
    /// </summary>
    public TimeoutBuilder<TState, TTrigger> After(TimeSpan timeout)
    {
        _config.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the trigger to fire when the timeout occurs.
    /// </summary>
    public TimeoutBuilder<TState, TTrigger> TransitionTo(TTrigger trigger)
    {
        _config.TimeoutTrigger = trigger;
        return this;
    }

    /// <summary>
    /// Uses a durable reminder for timeouts longer than 5 minutes.
    /// </summary>
    public TimeoutBuilder<TState, TTrigger> UseDurableReminder()
    {
        _config.UseDurableReminder = true;
        return this;
    }

    /// <summary>
    /// Uses a non-durable timer for short timeouts.
    /// </summary>
    public TimeoutBuilder<TState, TTrigger> UseTimer()
    {
        _config.UseDurableReminder = false;
        return this;
    }

    /// <summary>
    /// Makes the timer repeat until cancelled.
    /// </summary>
    public TimeoutBuilder<TState, TTrigger> Repeat()
    {
        _config.IsRepeating = true;
        return this;
    }

    /// <summary>
    /// Sets the timer name (defaults to state name if not set).
    /// </summary>
    public TimeoutBuilder<TState, TTrigger> WithName(string name)
    {
        _config.Name = name;
        return this;
    }

    /// <summary>
    /// Adds metadata to the timer.
    /// </summary>
    public TimeoutBuilder<TState, TTrigger> WithMetadata(string key, object value)
    {
        _config.Metadata ??= new Dictionary<string, object>();
        _config.Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the timer configuration.
    /// </summary>
    public TimerConfiguration Build()
    {
        if (string.IsNullOrEmpty(_config.Name))
        {
            _config.Name = $"Timeout_{_state}_{_config.TimeoutTrigger}";
        }

        // Auto-select durable reminder for timeouts > 5 minutes
        if (!_config.UseDurableReminder && _config.Timeout > TimeSpan.FromMinutes(5))
        {
            _config.UseDurableReminder = true;
        }

        return _config;
    }
}