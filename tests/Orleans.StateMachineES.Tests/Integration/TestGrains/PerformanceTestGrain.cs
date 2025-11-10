using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.StateMachineES.EventSourcing;
using Orleans.StateMachineES.EventSourcing.Configuration;
using Orleans.StateMachineES.EventSourcing.Events;
using Stateless;

namespace Orleans.StateMachineES.Tests.Integration.TestGrains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
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
[Alias("Orleans.StateMachineES.Tests.Integration.TestGrains.PerformanceTestState")]
public class PerformanceTestState : EventSourcedStateMachineState<PerformanceState>
{
    [Id(0)] public new int TransitionCount { get; set; }
}

[LogConsistencyProvider(ProviderName = "LogStorage")]
[StorageProvider(ProviderName = "Default")]
public class ResilientWorkflowGrain :
    EventSourcedStateMachineGrain<ResilientState, ResilientTrigger, ResilientWorkflowState>,
    IResilientWorkflowGrain
{
    private ILogger<ResilientWorkflowGrain>? _logger;
    
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = ServiceProvider.GetService(typeof(ILogger<ResilientWorkflowGrain>)) as ILogger<ResilientWorkflowGrain>;
    }

    protected override void ConfigureEventSourcing(EventSourcingOptions options)
    {
        // Enable auto-confirm to ensure state is persisted
        options.AutoConfirmEvents = true;
        options.EnableSnapshots = false; // Disable snapshots for simpler testing
    }
    
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
        // Just ensure the grain is activated - state restoration is handled by base class
        _logger?.LogDebug("ResilientWorkflowGrain {GrainId} initialized", this.GetPrimaryKeyString());
        await Task.CompletedTask;
    }

    public async Task ProcessStepAsync(string stepName)
    {
        _logger?.LogDebug("Processing step {StepName} in state {State}", stepName, StateMachine.State);
        
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

        // The FireAsync method will record the event and update State.CurrentState
        // The event count will be updated in the RecordTransitionEvent override
        State.LastStepProcessed = stepName;
        
        _logger?.LogDebug("Completed step {StepName}, new state: {NewState}, event count: {EventCount}", 
            stepName, StateMachine.State, State.EventCount);
    }

    public new Task<string> GetStateAsync()
    {
        return Task.FromResult(StateMachine.State.ToString());
    }

    public Task<int> GetEventCountAsync()
    {
        return Task.FromResult(State.EventCount);
    }

    protected override async Task RecordTransitionEvent(
        ResilientState fromState, 
        ResilientState toState, 
        ResilientTrigger trigger, 
        string? dedupeKey,
        Dictionary<string, object>? metadata = null)
    {
        // Update our custom event count first
        State.EventCount++;
        
        // Call the base implementation 
        await base.RecordTransitionEvent(fromState, toState, trigger, dedupeKey, metadata);
        
        _logger?.LogDebug("Recorded transition event: {From} -> {To} via {Trigger}, EventCount: {EventCount}", 
            fromState, toState, trigger, State.EventCount);
    }

    protected override async Task ReplayEventsAsync()
    {
        try
        {
            _logger?.LogDebug("Starting event replay for ResilientWorkflowGrain {GrainId}", this.GetPrimaryKeyString());

            // Get the events from the journal
            var events = await RetrieveConfirmedEvents(0, Version);
            
            if (events != null && events.Any())
            {
                _logger?.LogDebug("Replaying {Count} events for resilient grain", events.Count());
                
                State.EventCount = 0; // Reset counter before replay
                
                foreach (var evt in events)
                {
                    if (evt is StateTransitionEvent<ResilientState, ResilientTrigger> transitionEvent)
                    {
                        // Update state tracking
                        State.CurrentState = transitionEvent.ToState;
                        State.LastTransitionTimestamp = transitionEvent.Timestamp;
                        State.TransitionCount++;
                        State.EventCount++; // Update our custom event count
                    }
                }
                
                _logger?.LogInformation("Successfully replayed {Count} events, current state: {State}, event count: {EventCount}", 
                    events.Count(), State.CurrentState, State.EventCount);
            }
            else
            {
                _logger?.LogDebug("No events to replay for resilient grain");
                State.EventCount = 0;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to replay events for resilient grain");
            // Don't throw - we can still function without perfect replay
        }
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
[Alias("Orleans.StateMachineES.Tests.Integration.TestGrains.ResilientWorkflowState")]
public class ResilientWorkflowState : EventSourcedStateMachineState<ResilientState>
{
    [Id(0)] public int EventCount { get; set; }
    [Id(1)] public string LastStepProcessed { get; set; } = "";
}