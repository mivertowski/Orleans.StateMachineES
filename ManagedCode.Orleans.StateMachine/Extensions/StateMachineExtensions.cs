using System;
using System.Threading.Tasks;
using Stateless;

namespace ManagedCode.Orleans.StateMachine.Extensions;

public static class StateMachineExtensions
{
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryAsync(() => Task.Factory.StartNew(entryAction).Unwrap(), entryActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<StateMachine<TState, TEvent>.Transition, Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryAsync(t => Task.Factory.StartNew(() => entryAction(t)).Unwrap(), entryActionDescription);
    }
    
    public static StateMachine<TState, TEvent>.StateConfiguration OnExitOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<Task> exitAction, string exitActionDescription = null)
    {
        return machine.OnExitAsync(() => Task.Factory.StartNew(exitAction).Unwrap(), exitActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnExitOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<StateMachine<TState, TEvent>.Transition, Task> exitAction, string exitActionDescription = null)
    {
        return machine.OnExitAsync(t => Task.Factory.StartNew(() => exitAction(t)).Unwrap(), exitActionDescription);
    }
    
    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        TEvent trigger, Func<Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, () => Task.Factory.StartNew(entryAction).Unwrap(), entryActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        TEvent trigger, Func<StateMachine<TState, TEvent>.Transition, Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, t => Task.Factory.StartNew(() => entryAction(t)).Unwrap(), entryActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0> trigger, Func<TArg0, Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, arg0 => Task.Factory.StartNew(() => entryAction(arg0)).Unwrap(), entryActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0> trigger, Func<TArg0, StateMachine<TState, TEvent>.Transition, Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, t) => Task.Factory.StartNew(() => entryAction(arg0, t)).Unwrap(), entryActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0, TArg1>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0, TArg1> trigger, Func<TArg0, TArg1, Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, arg1) => Task.Factory.StartNew(() => entryAction(arg0, arg1)).Unwrap(), entryActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0, TArg1>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0, TArg1> trigger, Func<TArg0, TArg1, StateMachine<TState, TEvent>.Transition, Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, arg1, t) => Task.Factory.StartNew(() => entryAction(arg0, arg1, t)).Unwrap(), entryActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0, TArg1, TArg2>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0, TArg1, TArg2> trigger, Func<TArg0, TArg1, TArg2, Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, arg1, arg2) => Task.Factory.StartNew(() => entryAction(arg0, arg1, arg2)).Unwrap(), entryActionDescription);
    }

    public static StateMachine<TState, TEvent>.StateConfiguration OnEntryFromOrleansContextAsync<TState, TEvent, TArg0, TArg1, TArg2>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        StateMachine<TState, TEvent>.TriggerWithParameters<TArg0, TArg1, TArg2> trigger, Func<TArg0, TArg1, TArg2, StateMachine<TState, TEvent>.Transition, Task> entryAction, string entryActionDescription = null)
    {
        return machine.OnEntryFromAsync(trigger, (arg0, arg1, arg2, t) => Task.Factory.StartNew(() => entryAction(arg0, arg1, arg2, t)).Unwrap(), entryActionDescription);
    }
    
    public static StateMachine<TState, TEvent>.StateConfiguration OnActivateOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<Task> activateAction, string activateActionDescription = null)
    {
        return machine.OnActivateAsync(() => Task.Factory.StartNew(activateAction).Unwrap(), activateActionDescription);
    }
    
    public static StateMachine<TState, TEvent>.StateConfiguration OnDeactivateOrleansContextAsync<TState, TEvent>(
        this StateMachine<TState, TEvent>.StateConfiguration machine,
        Func<Task> deactivateAction, string deactivateActionDescription = null)
    {
        return machine.OnDeactivateAsync(() => Task.Factory.StartNew(deactivateAction).Unwrap(), deactivateActionDescription);
    }
}
