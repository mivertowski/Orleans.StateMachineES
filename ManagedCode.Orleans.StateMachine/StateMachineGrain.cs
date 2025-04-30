using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ManagedCode.Orleans.StateMachine.Interfaces;
using ManagedCode.Orleans.StateMachine.Models;
using Orleans;
using Stateless;
using Stateless.Graph;

namespace ManagedCode.Orleans.StateMachine;

public abstract class StateMachineGrain<TState, TTrigger> : Grain, IStateMachineGrain<TState, TTrigger>
{
    protected readonly StateMachine<TState, TTrigger>.StateConfiguration _stateConfiguration;
    protected StateMachine<TState, TTrigger> StateMachine { get; private set; }

    public Task ActivateAsync()
    {
        return StateMachine.ActivateAsync();
    }

    public Task DeactivateAsync()
    {
        return StateMachine.DeactivateAsync();
    }

    public Task FireAsync(TTrigger trigger)
    {
        return StateMachine.FireAsync(trigger);
    }

    public async Task FireAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        await StateMachine.FireAsync(tp, arg0);
    }

    public async Task FireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        await StateMachine.FireAsync(tp, arg0, arg1);
    }

    public async Task FireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        await StateMachine.FireAsync(tp, arg0, arg1, arg2);
    }

    public Task<TState> GetStateAsync()
    {
        return Task.FromResult(StateMachine.State);
    }

    public Task<bool> IsInStateAsync(TState state)
    {
        return Task.FromResult(StateMachine.IsInState(state));
    }

    public Task<bool> CanFireAsync(TTrigger trigger)
    {
        return Task.FromResult(StateMachine.CanFire(trigger));
    }

    public Task<OrleansStateMachineInfo> GetInfoAsync()
    {
        return Task.FromResult(new OrleansStateMachineInfo(StateMachine.GetInfo()));
    }

    public Task<IEnumerable<TTrigger>> GetPermittedTriggersAsync(params object[] args)
    {
        return Task.FromResult(StateMachine.GetPermittedTriggers(args));
    }

    public Task<IEnumerable<TriggerDetails<TState, TTrigger>>> GetDetailedPermittedTriggersAsync(params object[] args)
    {
        return Task.FromResult(StateMachine.GetDetailedPermittedTriggers(args));
    }

    public Task<IEnumerable<TTrigger>> GetPermittedTriggersPropertyAsync()
    {
        return Task.FromResult(StateMachine.PermittedTriggers);
    }

    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync(TTrigger trigger)
    {
        var result = StateMachine.CanFire(trigger, out var unmetGuards);
        return Task.FromResult((result, unmetGuards));
    }

    public Task<bool> CanFireAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        return Task.FromResult(StateMachine.CanFire(tp, arg0));
    }

    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        var result = StateMachine.CanFire(tp, arg0, out var unmet);
        return Task.FromResult((result, unmet));
    }

    public Task<bool> CanFireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        return Task.FromResult(StateMachine.CanFire(tp, arg0, arg1));
    }

    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        var result = StateMachine.CanFire(tp, arg0, arg1, out var unmet);
        return Task.FromResult((result, unmet));
    }

    public Task<bool> CanFireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        return Task.FromResult(StateMachine.CanFire(tp, arg0, arg1, arg2));
    }

    public Task<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        var result = StateMachine.CanFire(tp, arg0, arg1, arg2, out var unmet);
        return Task.FromResult((result, unmet));
    }
    
    public Task<string> ToStringAsync()
    {
        return Task.FromResult(StateMachine.ToString());
    }

    protected abstract StateMachine<TState, TTrigger> BuildStateMachine();

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        StateMachine = BuildStateMachine();

        return base.OnActivateAsync(cancellationToken);
    }
}
