using System;
using System.Threading.Tasks;
using Orleans;
using Orleans.Providers;
using Orleans.StateMachineES.EventSourcing;
using Stateless;

namespace Orleans.StateMachineES.Tests.Integration.TestGrains;

[StorageProvider(ProviderName = "Default")]
public class PerformanceTestGrain : 
    EventSourcedStateMachineGrain<PerformanceState, PerformanceTrigger, PerformanceTestState>,
    IPerformanceTestGrain
{
    private int _transitionCount = 0;

    protected override StateMachine<PerformanceState, PerformanceTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<PerformanceState, PerformanceTrigger>(PerformanceState.Ready);

        machine.Configure(PerformanceState.Ready)
            .Permit(PerformanceTrigger.Start, PerformanceState.Running);

        machine.Configure(PerformanceState.Running)
            .Permit(PerformanceTrigger.Process, PerformanceState.Processing)
            .Permit(PerformanceTrigger.Stop, PerformanceState.Stopped);

        machine.Configure(PerformanceState.Processing)
            .Permit(PerformanceTrigger.Complete, PerformanceState.Running)
            .Permit(PerformanceTrigger.Stop, PerformanceState.Stopped)
            .OnEntry(() => _transitionCount++);

        machine.Configure(PerformanceState.Stopped);

        return machine;
    }

    public async Task InitializeAsync()
    {
        State.TransitionCount = 0;
        await Task.CompletedTask;
    }

    public async Task TransitionAsync()
    {
        switch (StateMachine.State)
        {
            case PerformanceState.Ready:
                await FireAsync(PerformanceTrigger.Start);
                break;
            case PerformanceState.Running:
                await FireAsync(PerformanceTrigger.Process);
                break;
            case PerformanceState.Processing:
                await FireAsync(PerformanceTrigger.Complete);
                break;
        }

        State.TransitionCount++;
    }
}

public enum PerformanceState
{
    Ready, Running, Processing, Stopped
}

public enum PerformanceTrigger
{
    Start, Process, Complete, Stop
}

[GenerateSerializer]
public class PerformanceTestState : EventSourcedStateMachineState<PerformanceState>
{
    [Id(0)] public new int TransitionCount { get; set; }
}

[StorageProvider(ProviderName = "Default")]
public class ResilientWorkflowGrain : 
    EventSourcedStateMachineGrain<ResilientState, ResilientTrigger, ResilientWorkflowState>,
    IResilientWorkflowGrain
{
    protected override StateMachine<ResilientState, ResilientTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<ResilientState, ResilientTrigger>(ResilientState.Initial);

        machine.Configure(ResilientState.Initial)
            .Permit(ResilientTrigger.Start, ResilientState.Step1);

        machine.Configure(ResilientState.Step1)
            .Permit(ResilientTrigger.Next, ResilientState.Step2);

        machine.Configure(ResilientState.Step2)
            .Permit(ResilientTrigger.Next, ResilientState.Step3);

        machine.Configure(ResilientState.Step3)
            .Permit(ResilientTrigger.Complete, ResilientState.Completed);

        machine.Configure(ResilientState.Completed);

        return machine;
    }

    public async Task InitializeAsync()
    {
        State.CurrentState = ResilientState.Initial;
        State.EventCount = 0;
        await Task.CompletedTask;
    }

    public async Task ProcessStepAsync(string stepName)
    {
        switch (StateMachine.State)
        {
            case ResilientState.Initial:
                await FireAsync(ResilientTrigger.Start);
                break;
            case ResilientState.Step1:
            case ResilientState.Step2:
                await FireAsync(ResilientTrigger.Next);
                break;
            case ResilientState.Step3:
                await FireAsync(ResilientTrigger.Complete);
                break;
        }

        State.EventCount++;
        State.LastStepProcessed = stepName;
    }

    public new Task<string> GetStateAsync()
    {
        return Task.FromResult(StateMachine.State.ToString());
    }

    public Task<int> GetEventCountAsync()
    {
        return Task.FromResult(State.EventCount);
    }

    public new async Task DeactivateAsync()
    {
        // Force grain deactivation
        DeactivateOnIdle();
        await Task.CompletedTask;
    }
}

public enum ResilientState
{
    Initial, Step1, Step2, Step3, Completed
}

public enum ResilientTrigger
{
    Start, Next, Complete
}

[GenerateSerializer]
public class ResilientWorkflowState : EventSourcedStateMachineState<ResilientState>
{
    [Id(0)] public int EventCount { get; set; }
    [Id(1)] public string LastStepProcessed { get; set; } = "";
}