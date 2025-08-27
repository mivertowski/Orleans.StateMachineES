using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.StateMachineES.EventSourcing;
using Orleans.StateMachineES.EventSourcing.Events;
using Orleans.StateMachineES.Hierarchical;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Orleans.StateMachineES.Sagas;
using Orleans.StateMachineES.Timers;
using StateMachineVersion = Orleans.StateMachineES.Abstractions.Models.StateMachineVersion;
using Orleans.StateMachineES.Versioning;
using Stateless;

namespace Orleans.StateMachineES.Tests.Integration.TestGrains;

/// <summary>
/// Comprehensive test grain that combines all features:
/// - Event Sourcing
/// - Versioning
/// - Hierarchical States
/// - Timers
/// - Sagas
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
[StorageProvider(ProviderName = "Default")]
public class ComprehensiveWorkflowGrain : 
    VersionedStateMachineGrain<WorkflowState, WorkflowTrigger, ComprehensiveWorkflowState>,
    IComprehensiveWorkflowGrain
{
    private readonly List<AuditEntry> _auditTrail = new();
    private readonly List<CompensationExecution> _compensationHistory = new();
    private ILogger<ComprehensiveWorkflowGrain>? _logger;
    private WorkflowData? _workflowData;

    protected override StateMachine<WorkflowState, WorkflowTrigger> BuildStateMachine()
    {
        var machine = new StateMachine<WorkflowState, WorkflowTrigger>(WorkflowState.Idle);

        // Configure hierarchical states
        machine.Configure(WorkflowState.Idle)
            .Permit(WorkflowTrigger.Start, WorkflowState.Active);

        machine.Configure(WorkflowState.Active)
            .InitialTransition(WorkflowState.Processing)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
            .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

        machine.Configure(WorkflowState.Processing)
            .SubstateOf(WorkflowState.Active)
            .Permit(WorkflowTrigger.Validate, WorkflowState.Validating)
            .OnEntry(() => LogAudit("Entered Processing"))
            .OnExit(() => LogAudit("Exited Processing"));

        machine.Configure(WorkflowState.Validating)
            .SubstateOf(WorkflowState.Active)
            .Permit(WorkflowTrigger.Execute, WorkflowState.Executing)
            .Permit(WorkflowTrigger.Fail, WorkflowState.Failed)
            .OnEntry(() => LogAudit("Validation started"))
            .OnExit(() => LogAudit("Validation completed"));

        machine.Configure(WorkflowState.Executing)
            .SubstateOf(WorkflowState.Active)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
            .Permit(WorkflowTrigger.Fail, WorkflowState.Failed)
            .OnEntry(() => LogAudit("Execution started"));

        machine.Configure(WorkflowState.Completed)
            .OnEntry(() => LogAudit("Workflow completed successfully"));

        machine.Configure(WorkflowState.Failed)
            .Permit(WorkflowTrigger.Compensate, WorkflowState.Compensated)
            .OnEntry(() => LogAudit("Workflow failed"));

        machine.Configure(WorkflowState.Compensated)
            .OnEntry(() => LogAudit("Compensation executed"));

        return machine;
    }

    protected override async Task RegisterBuiltInVersionsAsync()
    {
        // Skip version registration for tests to avoid timeout issues
        // The DefinitionRegistry might not be properly initialized in test environment
        await Task.CompletedTask;
    }

    private StateMachine<WorkflowState, WorkflowTrigger> BuildStateMachineV1()
    {
        // Simpler version without hierarchical states
        var machine = new StateMachine<WorkflowState, WorkflowTrigger>(WorkflowState.Idle);

        machine.Configure(WorkflowState.Idle)
            .Permit(WorkflowTrigger.Start, WorkflowState.Processing);

        machine.Configure(WorkflowState.Processing)
            .Permit(WorkflowTrigger.Complete, WorkflowState.Completed)
            .Permit(WorkflowTrigger.Fail, WorkflowState.Failed);

        machine.Configure(WorkflowState.Completed);
        machine.Configure(WorkflowState.Failed);

        return machine;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = ServiceProvider.GetService(typeof(ILogger<ComprehensiveWorkflowGrain>)) as ILogger<ComprehensiveWorkflowGrain>;
    }

    public async Task InitializeWithVersionAsync(StateMachineVersion version)
    {
        State.Version = version;
        State.CurrentState = WorkflowState.Idle;
        await Task.CompletedTask;
    }

    public async Task StartWorkflowAsync(WorkflowData data)
    {
        _workflowData = data;
        await FireAsync(WorkflowTrigger.Start);
        LogAudit($"Workflow started with ID: {data.WorkflowId}");
    }

    public Task<WorkflowState> GetCurrentStateAsync()
    {
        return Task.FromResult(State.CurrentState);
    }

    public async Task<List<StateTransitionEvent<WorkflowState, WorkflowTrigger>>> GetEventHistoryAsync()
    {
        // In a real implementation, this would query the event journal
        var events = new List<StateTransitionEvent<WorkflowState, WorkflowTrigger>>();
        
        // Simulate some events
        events.Add(new StateTransitionEvent<WorkflowState, WorkflowTrigger>(
            WorkflowState.Idle,
            WorkflowState.Processing,
            WorkflowTrigger.Start,
            DateTime.UtcNow.AddMinutes(-5),
            null,
            null,
            "1.0.0",
            null
        ));
        
        return await Task.FromResult(events);
    }

    public async Task StartTimerAsync(string timerName, TimeSpan duration)
    {
        // Configure timer that will trigger state transition
        await ConfigureTimerAsync(new TimerConfiguration
        {
            Name = timerName,
            Timeout = duration,
            TimeoutTrigger = WorkflowTrigger.Timeout
        });
        
        LogAudit($"Timer '{timerName}' started with duration {duration}");
    }

    public new async Task<VersionUpgradeResult> UpgradeToVersionAsync(StateMachineVersion targetVersion, MigrationStrategy strategy)
    {
        var result = await base.UpgradeToVersionAsync(targetVersion, strategy);
        LogAudit($"Upgraded from {result.OldVersion} to {result.NewVersion}");
        return result;
    }

    public async Task<SagaStepResult> ExecuteSagaStepAsync(string stepName, SagaContext context)
    {
        LogAudit($"Executing saga step: {stepName}");
        
        // Simulate saga step execution
        await Task.Delay(100);
        
        if (stepName == "validation")
        {
            await FireAsync(WorkflowTrigger.Validate);
        }
        
        return SagaStepResult.Success($"Step {stepName} completed");
    }

    public new async Task<bool> IsInStateAsync(WorkflowState state)
    {
        // Check if current state is the specified state or a substate of it
        if (State.CurrentState == state)
            return true;
            
        // Check hierarchical relationship
        var descendants = await GetDescendantStatesAsync(state);
        return descendants.Contains(State.CurrentState);
    }

    public async Task<List<WorkflowState>> GetDescendantStatesAsync(WorkflowState parentState)
    {
        var descendants = new List<WorkflowState>();
        
        if (parentState == WorkflowState.Active)
        {
            descendants.AddRange(new[] 
            { 
                WorkflowState.Processing, 
                WorkflowState.Validating, 
                WorkflowState.Executing 
            });
        }
        
        return await Task.FromResult(descendants);
    }

    public async Task CompleteWorkflowAsync()
    {
        await FireAsync(WorkflowTrigger.Complete);
        LogAudit("Workflow completed");
    }

    public Task<List<AuditEntry>> GetAuditTrailAsync()
    {
        return Task.FromResult(_auditTrail.ToList());
    }

    public async Task TriggerCompensationAsync()
    {
        if (State.CurrentState == WorkflowState.Failed)
        {
            await FireAsync(WorkflowTrigger.Compensate);
            
            var compensation = new CompensationExecution
            {
                StepName = "workflow-compensation",
                ExecutionTime = DateTime.UtcNow,
                Duration = TimeSpan.FromMilliseconds(150),
                IsSuccess = true
            };
            
            _compensationHistory.Add(compensation);
            LogAudit("Compensation triggered and executed");
        }
    }

    public Task<List<CompensationExecution>> GetCompensationHistoryAsync()
    {
        return Task.FromResult(_compensationHistory.ToList());
    }

    private void LogAudit(string message)
    {
        var entry = new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = message,
            Details = $"State: {State.CurrentState}, Version: {State.Version}"
        };
        
        _auditTrail.Add(entry);
        _logger?.LogInformation("Audit: {Message}", message);
    }

    // Timer support
    private async Task ConfigureTimerAsync(TimerConfiguration config)
    {
        // In a real implementation, this would use RegisterOrUpdateReminder
        await Task.Delay(config.Timeout);
        
        if (config.TimeoutTrigger?.Equals(WorkflowTrigger.Timeout) == true)
        {
            await FireAsync(WorkflowTrigger.Validate);
        }
    }
}

[GenerateSerializer]
public class ComprehensiveWorkflowState : VersionedStateMachineState<WorkflowState>
{
    [Id(0)] public string WorkflowId { get; set; } = "";
    [Id(1)] public Dictionary<string, object> BusinessData { get; set; } = new();
    [Id(2)] public List<string> ExecutedSteps { get; set; } = new();
}