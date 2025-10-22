using Stateless;

namespace Orleans.StateMachineES.Extensions;

/// <summary>
/// Provides extension methods for configuring Stateless state machines to run entry, exit, and trigger actions
/// within the Orleans context, ensuring correct synchronization and execution semantics.
/// </summary>
public static class StateMachineExtensions
{
    /// <summary>
    /// Configures the state machine to execute the specified entry action within the Orleans context asynchronously.
    /// </summary>
    /// <typeparam name="TState">The type representing states.</typeparam>
    /// <typeparam name="TEvent">The type representing events.</typeparam>
    /// <param name="machine">The state configuration.</param>
    /// <param name="entryAction">The asynchronous entry action to execute.</param>
    /// <param name="entryActionDescription">Optional description of the entry action.</param>
    /// <returns>The updated state configuration.</returns>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryAsync(() => Task.Factory.StartNew(entryAction).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action with transition information within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<StateMachine<TState, TEvent>.Transition, Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryAsync(t => Task.Factory.StartNew(() => entryAction(t)).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified exit action within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnExitOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<Task> exitAction, string? exitActionDescription = null)
    {
        return machine.OnExitAsync(() => Task.Factory.StartNew(exitAction).Unwrap(), exitActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified exit action with transition information within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnExitOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<StateMachine<TState, TEvent>.Transition, Task> exitAction, string? exitActionDescription = null)
    {
        return machine.OnExitAsync(t => Task.Factory.StartNew(() => exitAction(t)).Unwrap(), exitActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action when entering from a specific trigger within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        TEvent trigger, Func<Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, () => Task.Factory.StartNew(entryAction).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action with transition information when entering from a specific trigger within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        TEvent trigger, Func<StateMachine<TState, TEvent>.Transition, Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, t => Task.Factory.StartNew(() => entryAction(t)).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action with one argument when entering from a parameterized trigger within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0> trigger, Func<TArg0, Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, arg0 => Task.Factory.StartNew(() => entryAction(arg0)).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action with one argument and transition information when entering from a parameterized trigger within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0> trigger, Func<TArg0, StateMachine<TState, TEvent>.Transition, Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, t) => Task.Factory.StartNew(() => entryAction(arg0, t)).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action with two arguments when entering from a parameterized trigger within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0, TArg1>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0, TArg1> trigger, Func<TArg0, TArg1, Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, arg1) => Task.Factory.StartNew(() => entryAction(arg0, arg1)).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action with two arguments and transition information when entering from a parameterized trigger within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0, TArg1>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0, TArg1> trigger, Func<TArg0, TArg1, StateMachine<TState, TEvent>.Transition, Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, arg1, t) => Task.Factory.StartNew(() => entryAction(arg0, arg1, t)).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action with three arguments when entering from a parameterized trigger within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0, TArg1, TArg2>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0, TArg1, TArg2> trigger, Func<TArg0, TArg1, TArg2, Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, arg1, arg2) => Task.Factory.StartNew(() => entryAction(arg0, arg1, arg2)).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified entry action with three arguments and transition information when entering from a parameterized trigger within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0, TArg1, TArg2>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0, TArg1, TArg2> trigger, Func<TArg0, TArg1, TArg2, StateMachine<TState, TEvent>.Transition, Task> entryAction, string? entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, arg1, arg2, t) => Task.Factory.StartNew(() => entryAction(arg0, arg1, arg2, t)).Unwrap(), entryActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified activation action within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnActivateOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<Task> activateAction, string? activateActionDescription = null)
    {
        return machine.OnActivateAsync(() => Task.Factory.StartNew(activateAction).Unwrap(), activateActionDescription);
    }

    /// <summary>
    /// Configures the state machine to execute the specified deactivation action within the Orleans context asynchronously.
    /// </summary>
    public static StateMachine<TState, TEvent>.StateConfiguration OnDeactivateOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<Task> deactivateAction, string? deactivateActionDescription = null)
    {
        return machine.OnDeactivateAsync(() => Task.Factory.StartNew(deactivateAction).Unwrap(), deactivateActionDescription);
    }
}