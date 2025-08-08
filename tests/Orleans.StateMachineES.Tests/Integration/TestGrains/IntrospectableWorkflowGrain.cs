using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Orleans.StateMachineES.Versioning;
using Orleans.StateMachineES.Tests.Integration;
using Stateless;

namespace Orleans.StateMachineES.Tests.Integration.TestGrains;

/// <summary>
/// Grain for testing introspection capabilities of state machines.
/// </summary>
public class IntrospectableWorkflowGrain : StateMachineGrain<WorkflowState, WorkflowTrigger>, IIntrospectableWorkflowGrain
{
    private ILogger<IntrospectableWorkflowGrain>? _logger;

    protected override StateMachine<WorkflowState, WorkflowTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<WorkflowState, WorkflowTrigger>(WorkflowState.Idle);

        machine.Configure(WorkflowState.Idle)
            .Permit(WorkflowTrigger.Start, WorkflowState.Active)
            .OnEntry(() => LogTransition("Entered Idle state"));

        machine.Configure(WorkflowState.Active)
            .Permit(WorkflowTrigger.Process, WorkflowState.Processing)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
            .Permit(WorkflowTrigger.Fail, WorkflowState.Failed)
            .OnEntry(() => LogTransition("Entered Active state"))
            .OnExit(() => LogTransition("Exited Active state"));

        machine.Configure(WorkflowState.Processing)
            .Permit(WorkflowTrigger.Validate, WorkflowState.Validating)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
            .Permit(WorkflowTrigger.Fail, WorkflowState.Failed)
            .OnEntry(() => LogTransition("Entered Processing state"))
            .OnExit(() => LogTransition("Exited Processing state"));

        machine.Configure(WorkflowState.Validating)
            .Permit(WorkflowTrigger.Execute, WorkflowState.Executing)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
            .Permit(WorkflowTrigger.Fail, WorkflowState.Failed)
            .OnEntry(() => LogTransition("Entered Validating state"));

        machine.Configure(WorkflowState.Executing)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
            .Permit(WorkflowTrigger.Fail, WorkflowState.Failed)
            .OnEntry(() => LogTransition("Entered Executing state"));

        machine.Configure(WorkflowState.Completed)
            .OnEntry(() => LogTransition("Workflow completed successfully"));

        machine.Configure(WorkflowState.Failed)
            .Permit(WorkflowTrigger.Compensate, WorkflowState.Compensated)
            .OnEntry(() => LogTransition("Workflow failed"));

        machine.Configure(WorkflowState.Compensated)
            .OnEntry(() => LogTransition("Compensation executed"));

        return machine;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = ServiceProvider.GetService(typeof(ILogger<IntrospectableWorkflowGrain>)) as ILogger<IntrospectableWorkflowGrain>;
    }

    public async Task InitializeAsync()
    {
        // Initialize the grain to the starting state
        await Task.CompletedTask;
    }

    public async Task<OrleansStateMachineInfo> GetStateMachineInfoAsync()
    {
        // Use the actual StateMachine to get StateMachineInfo
        var stateMachineInfo = StateMachine.GetInfo();
        
        // Create OrleansStateMachineInfo from the Stateless StateMachineInfo
        var info = new OrleansStateMachineInfo(stateMachineInfo);

        return info;
    }

    public async Task<EnhancedStateMachineConfiguration<WorkflowState, WorkflowTrigger>> GetDetailedConfigurationAsync()
    {
        var config = new EnhancedStateMachineConfiguration<WorkflowState, WorkflowTrigger>
        {
            InitialState = WorkflowState.Idle,
            IsValid = true,
            ExtractionTimestamp = DateTime.UtcNow
        };

        // Build states dictionary
        foreach (var state in Enum.GetValues<WorkflowState>())
        {
            config.States[state] = new EnhancedStateConfiguration<WorkflowState, WorkflowTrigger>
            {
                State = state,
                IsInitialState = state == WorkflowState.Idle,
                Substates = new List<WorkflowState>(),
                Superstate = null,
                PermittedTriggers = new HashSet<WorkflowTrigger>(),
                ActivableTriggers = new HashSet<WorkflowTrigger>(),
                IgnoredTriggers = new HashSet<WorkflowTrigger>(),
                Transitions = new List<EnhancedTransitionConfiguration<WorkflowState, WorkflowTrigger>>(),
                Metadata = new Dictionary<string, object>()
            };
        }

        // Build transition map
        var transitions = new List<(WorkflowState From, WorkflowTrigger Trigger, WorkflowState To)>
        {
            (WorkflowState.Idle, WorkflowTrigger.Start, WorkflowState.Active),
            (WorkflowState.Active, WorkflowTrigger.Process, WorkflowState.Processing),
            (WorkflowState.Active, WorkflowTrigger.Complete, WorkflowState.Completed),
            (WorkflowState.Active, WorkflowTrigger.Fail, WorkflowState.Failed),
            (WorkflowState.Processing, WorkflowTrigger.Validate, WorkflowState.Validating),
            (WorkflowState.Processing, WorkflowTrigger.Complete, WorkflowState.Completed),
            (WorkflowState.Processing, WorkflowTrigger.Fail, WorkflowState.Failed),
            (WorkflowState.Validating, WorkflowTrigger.Execute, WorkflowState.Executing),
            (WorkflowState.Validating, WorkflowTrigger.Complete, WorkflowState.Completed),
            (WorkflowState.Validating, WorkflowTrigger.Fail, WorkflowState.Failed),
            (WorkflowState.Executing, WorkflowTrigger.Complete, WorkflowState.Completed),
            (WorkflowState.Executing, WorkflowTrigger.Fail, WorkflowState.Failed),
            (WorkflowState.Failed, WorkflowTrigger.Compensate, WorkflowState.Compensated)
        };

        foreach (var (from, trigger, to) in transitions)
        {
            var key = (from, trigger);
            if (!config.TransitionMap.ContainsKey(key))
            {
                config.TransitionMap[key] = new List<EnhancedTransitionConfiguration<WorkflowState, WorkflowTrigger>>();
            }
            
            config.TransitionMap[key].Add(new EnhancedTransitionConfiguration<WorkflowState, WorkflowTrigger>
            {
                SourceState = from,
                DestinationState = to,
                Trigger = trigger,
                HasGuard = false,
                GuardDescription = null,
                IsInternal = false,
                IsReentrant = false
            });
        }

        return config;
    }

    public async Task<string> GetDotGraphAsync()
    {
        var config = await GetDetailedConfigurationAsync();
        var currentState = await GetStateAsync();
        
        var dotGraph = "digraph StateMachine {\n";
        dotGraph += "  rankdir=LR;\n";
        dotGraph += "  node [shape=circle];\n";
        
        // Add initial state marker
        dotGraph += $"  \"{config.InitialState}\" [shape=doublecircle];\n";
        
        // Add final states (completed and compensated are final)
        var finalStates = new[] { WorkflowState.Completed, WorkflowState.Compensated };
        foreach (var state in finalStates)
        {
            dotGraph += $"  \"{state}\" [shape=doublecircle];\n";
        }
        
        // Add current state highlighting
        dotGraph += $"  \"{currentState}\" [style=filled, fillcolor=yellow];\n";
        
        // Add transitions
        foreach (var transitionGroup in config.TransitionMap)
        {
            var (source, trigger) = transitionGroup.Key;
            foreach (var transition in transitionGroup.Value)
            {
                dotGraph += $"  \"{source}\" -> \"{transition.DestinationState}\" [label=\"{trigger}\"];\n";
            }
        }
        
        dotGraph += "}";
        
        return dotGraph;
    }

    public async Task<StateMachineComparison<WorkflowState, WorkflowTrigger>> CompareWithVersionAsync(StateMachineVersion version)
    {
        var currentConfig = await GetDetailedConfigurationAsync();
        
        // For this test implementation, we'll create a simulated comparison
        var comparison = new StateMachineComparison<WorkflowState, WorkflowTrigger>
        {
            Config1 = currentConfig,
            Config2 = currentConfig, // Simplified for testing
            SimilarityScore = 0.85, // 85% similar
            AddedStates = new List<WorkflowState>(),
            RemovedStates = new List<WorkflowState>(),
            CommonStates = new List<WorkflowState> { WorkflowState.Idle, WorkflowState.Active, WorkflowState.Processing },
            AddedTransitions = new List<(WorkflowState State, WorkflowTrigger Trigger)>
            {
                (WorkflowState.Failed, WorkflowTrigger.Compensate)
            },
            RemovedTransitions = new List<(WorkflowState State, WorkflowTrigger Trigger)>()
        };

        return comparison;
    }

    private void LogTransition(string message)
    {
        _logger?.LogDebug("Introspectable Grain {GrainId}: {Message}", this.GetPrimaryKeyString(), message);
    }
}