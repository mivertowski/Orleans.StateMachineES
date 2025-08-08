using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.StateMachineES.EventSourcing;
using Orleans.StateMachineES.EventSourcing.Events;
using Orleans.StateMachineES.Hierarchical;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Orleans.StateMachineES.Sagas;
using Orleans.StateMachineES.Tests.Cluster;
using Orleans.StateMachineES.Timers;
using Orleans.StateMachineES.Versioning;
using Stateless;
using Xunit;
using Xunit.Abstractions;

namespace Orleans.StateMachineES.Tests.Integration;

/// <summary>
/// Comprehensive integration tests that verify all features working together.
/// Tests the complete lifecycle of complex state machines with all advanced features.
/// </summary>
[Collection(nameof(TestClusterApplication))]
public class ComprehensiveIntegrationTests
{
    private readonly TestClusterApplication _testApp;
    private readonly ITestOutputHelper _outputHelper;
    private readonly ILogger<ComprehensiveIntegrationTests> _logger;

    public ComprehensiveIntegrationTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
    {
        _testApp = testApp;
        _outputHelper = outputHelper;
        _logger = _testApp.Cluster.ServiceProvider.GetRequiredService<ILogger<ComprehensiveIntegrationTests>>();
    }

    [Fact(Skip = "Requires comprehensive workflow grain implementation - disabled for v1.0 release")]
    public async Task FullWorkflow_EventSourcedVersionedHierarchicalTimerSaga_ShouldWork()
    {
        // This test combines ALL features into one comprehensive workflow
        var grainId = $"comprehensive-test-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IComprehensiveWorkflowGrain>(grainId);
        
        // 1. Initialize with version 1.0.0
        await grain.InitializeWithVersionAsync(new StateMachineVersion(1, 0, 0));
        _outputHelper.WriteLine("✅ Initialized with version 1.0.0");
        
        // 2. Start workflow (tests hierarchical states)
        await grain.StartWorkflowAsync(new WorkflowData 
        { 
            WorkflowId = grainId,
            BusinessData = new Dictionary<string, object>
            {
                ["OrderId"] = "ORD-123",
                ["Amount"] = 1000.00m,
                ["CustomerId"] = "CUST-456"
            }
        });
        
        var state = await grain.GetCurrentStateAsync();
        _outputHelper.WriteLine($"✅ Started workflow, current state: {state}");
        state.Should().Be(WorkflowState.Processing);
        
        // 3. Verify event sourcing is working
        var events = await grain.GetEventHistoryAsync();
        events.Should().NotBeEmpty();
        _outputHelper.WriteLine($"✅ Event sourcing working, {events.Count} events recorded");
        
        // 4. Test timer functionality
        await grain.StartTimerAsync("validation-timer", TimeSpan.FromSeconds(1));
        await Task.Delay(1500); // Wait for timer
        
        state = await grain.GetCurrentStateAsync();
        _outputHelper.WriteLine($"✅ Timer fired, current state: {state}");
        
        // 5. Upgrade to version 1.1.0 (test versioning with migration)
        var upgradeResult = await grain.UpgradeToVersionAsync(
            new StateMachineVersion(1, 1, 0), 
            MigrationStrategy.Automatic);
        
        upgradeResult.IsSuccess.Should().BeTrue();
        _outputHelper.WriteLine($"✅ Upgraded to version 1.1.0: {upgradeResult.MigrationSummary?.ChangesApplied.FirstOrDefault()}");
        
        // 6. Execute saga step (test saga orchestration)
        var sagaResult = await grain.ExecuteSagaStepAsync("validation", new SagaContext 
        { 
            CorrelationId = Guid.NewGuid().ToString(),
            BusinessTransactionId = $"BTX-{grainId}"
        });
        
        sagaResult.IsSuccess.Should().BeTrue();
        _outputHelper.WriteLine($"✅ Saga step executed successfully");
        
        // 7. Test hierarchical state navigation
        var isInParentState = await grain.IsInStateAsync(WorkflowState.Active);
        var descendants = await grain.GetDescendantStatesAsync(WorkflowState.Active);
        descendants.Should().Contain(WorkflowState.Processing);
        _outputHelper.WriteLine($"✅ Hierarchical states working, descendants: {string.Join(", ", descendants)}");
        
        // 8. Complete workflow
        await grain.CompleteWorkflowAsync();
        state = await grain.GetCurrentStateAsync();
        state.Should().Be(WorkflowState.Completed);
        _outputHelper.WriteLine("✅ Workflow completed successfully");
        
        // 9. Verify full audit trail
        var auditTrail = await grain.GetAuditTrailAsync();
        auditTrail.Should().NotBeEmpty();
        _outputHelper.WriteLine($"✅ Full audit trail available with {auditTrail.Count} entries");
        
        // 10. Test compensation if needed
        await grain.TriggerCompensationAsync();
        var compensationHistory = await grain.GetCompensationHistoryAsync();
        compensationHistory.Should().NotBeEmpty();
        _outputHelper.WriteLine($"✅ Compensation executed, {compensationHistory.Count} compensations recorded");
    }

    [Fact]
    public async Task PerformanceTest_MassiveStateTransitions_ShouldHandleLoad()
    {
        // Performance test with many concurrent state transitions
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var errorCount = 0;
        
        const int GrainCount = 10;
        const int TransitionsPerGrain = 100;
        
        _outputHelper.WriteLine($"Starting performance test: {GrainCount} grains × {TransitionsPerGrain} transitions");
        
        for (int i = 0; i < GrainCount; i++)
        {
            var grainId = $"perf-test-{i}";
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var grain = _testApp.Cluster.Client.GetGrain<IPerformanceTestGrain>(grainId);
                    await grain.InitializeAsync();
                    
                    for (int j = 0; j < TransitionsPerGrain; j++)
                    {
                        await grain.TransitionAsync();
                        Interlocked.Increment(ref successCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in performance test for grain {GrainId}", grainId);
                    Interlocked.Increment(ref errorCount);
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        var totalTransitions = successCount;
        var transitionsPerSecond = totalTransitions / stopwatch.Elapsed.TotalSeconds;
        
        _outputHelper.WriteLine($"Performance Results:");
        _outputHelper.WriteLine($"  Total transitions: {totalTransitions}");
        _outputHelper.WriteLine($"  Time taken: {stopwatch.Elapsed.TotalSeconds:F2}s");
        _outputHelper.WriteLine($"  Transitions/second: {transitionsPerSecond:F2}");
        _outputHelper.WriteLine($"  Errors: {errorCount}");
        
        successCount.Should().Be(GrainCount * TransitionsPerGrain);
        errorCount.Should().Be(0);
        transitionsPerSecond.Should().BeGreaterThan(100); // At least 100 transitions per second
    }

    [Fact]
    public async Task Resilience_GrainFailureAndRecovery_ShouldMaintainState()
    {
        // Test grain failure and recovery with state persistence
        var grainId = $"resilience-test-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IResilientWorkflowGrain>(grainId);
        
        // Setup initial state
        await grain.InitializeAsync();
        await grain.ProcessStepAsync("step1");
        await grain.ProcessStepAsync("step2");
        
        var stateBeforeFailure = await grain.GetStateAsync();
        var eventsBeforeFailure = await grain.GetEventCountAsync();
        
        _outputHelper.WriteLine($"State before failure: {stateBeforeFailure}, Events: {eventsBeforeFailure}");
        
        // Simulate grain deactivation (failure)
        await grain.DeactivateAsync();
        await Task.Delay(1000); // Give time for deactivation
        
        // Reactivate grain (recovery)
        var recoveredGrain = _testApp.Cluster.Client.GetGrain<IResilientWorkflowGrain>(grainId);
        var stateAfterRecovery = await recoveredGrain.GetStateAsync();
        var eventsAfterRecovery = await recoveredGrain.GetEventCountAsync();
        
        _outputHelper.WriteLine($"State after recovery: {stateAfterRecovery}, Events: {eventsAfterRecovery}");
        
        // Verify state was preserved
        stateAfterRecovery.Should().Be(stateBeforeFailure);
        eventsAfterRecovery.Should().Be(eventsBeforeFailure);
        
        // Continue processing to verify grain is fully functional
        await recoveredGrain.ProcessStepAsync("step3");
        var finalState = await recoveredGrain.GetStateAsync();
        
        _outputHelper.WriteLine($"Final state after recovery and continued processing: {finalState}");
        finalState.Should().NotBe(stateAfterRecovery); // Should have progressed
    }

    [Fact]
    public async Task ComplexSaga_MultiGrainCoordination_ShouldCompleteSuccessfully()
    {
        // Test complex saga with multiple grain coordination
        var sagaId = $"saga-test-{Guid.NewGuid():N}";
        var sagaGrain = _testApp.Cluster.Client.GetGrain<IComplexSagaGrain>(sagaId);
        
        var sagaData = new ComplexSagaData
        {
            SagaId = sagaId,
            Steps = new List<string> { "payment", "inventory", "shipping", "notification" },
            RequiresCompensation = false
        };
        
        // Execute saga
        var result = await sagaGrain.ExecuteAsync(sagaData, Guid.NewGuid().ToString());
        
        _outputHelper.WriteLine($"Saga execution result: {result.Status}");
        _outputHelper.WriteLine($"Steps completed: {string.Join(", ", result.CompletedSteps)}");
        
        result.IsSuccess.Should().BeTrue();
        result.CompletedSteps.Should().HaveCount(4);
        
        // Test compensation scenario
        sagaData.RequiresCompensation = true;
        var compensationResult = await sagaGrain.ExecuteAsync(sagaData, Guid.NewGuid().ToString());
        
        _outputHelper.WriteLine($"Saga with compensation result: {compensationResult.Status}");
        _outputHelper.WriteLine($"Compensated steps: {string.Join(", ", compensationResult.CompensatedSteps)}");
        
        compensationResult.IsCompensated.Should().BeTrue();
        compensationResult.CompensatedSteps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StateIntrospection_ComplexStateMachine_ShouldProvideFullVisibility()
    {
        // Test state machine introspection capabilities
        var grainId = $"introspection-test-{Guid.NewGuid():N}";
        var grain = _testApp.Cluster.Client.GetGrain<IIntrospectableWorkflowGrain>(grainId);
        
        await grain.InitializeAsync();
        
        // Get state machine info
        var info = await grain.GetStateMachineInfoAsync();
        info.Should().NotBeNull();
        info.States.Should().NotBeEmpty();
        
        _outputHelper.WriteLine($"State machine has {info.States.Count} states");
        
        // Get detailed configuration
        var config = await grain.GetDetailedConfigurationAsync();
        config.Should().NotBeNull();
        config.States.Should().NotBeEmpty();
        config.TransitionMap.Should().NotBeEmpty();
        
        _outputHelper.WriteLine($"Configuration extracted:");
        _outputHelper.WriteLine($"  States: {config.States.Count}");
        _outputHelper.WriteLine($"  Transitions: {config.TransitionMap.Count}");
        _outputHelper.WriteLine($"  Guarded triggers: {config.GuardedTriggers.Count}");
        
        // Test DOT graph generation
        var dotGraph = await grain.GetDotGraphAsync();
        dotGraph.Should().NotBeNullOrEmpty();
        dotGraph.Should().Contain("digraph");
        dotGraph.Should().Contain("->");
        
        _outputHelper.WriteLine("✅ DOT graph generated successfully");
        
        // Compare with another version
        var comparison = await grain.CompareWithVersionAsync(new StateMachineVersion(1, 0, 0));
        comparison.Should().NotBeNull();
        comparison.SimilarityScore.Should().BeInRange(0, 1);
        
        _outputHelper.WriteLine($"Similarity with v1.0.0: {comparison.SimilarityScore:P}");
    }

    [Fact]
    public async Task EndToEnd_OrderProcessingWorkflow_ShouldHandleCompleteLifecycle()
    {
        // Complete end-to-end order processing workflow
        var orderId = $"ORD-{Guid.NewGuid():N}";
        var orderGrain = _testApp.Cluster.Client.GetGrain<IOrderProcessingWorkflowGrain>(orderId);
        
        // Create order
        await orderGrain.CreateOrderAsync(new OrderData
        {
            OrderId = orderId,
            CustomerId = "CUST-789",
            Items = new List<OrderItem>
            {
                new() { ProductId = "PROD-1", Quantity = 2, Price = 50.00m },
                new() { ProductId = "PROD-2", Quantity = 1, Price = 100.00m }
            },
            TotalAmount = 200.00m
        });
        
        _outputHelper.WriteLine($"Order {orderId} created");
        
        // Process payment
        await orderGrain.ProcessPaymentAsync(new PaymentInfo
        {
            PaymentMethod = "CreditCard",
            TransactionId = $"TXN-{Guid.NewGuid():N}"
        });
        
        _outputHelper.WriteLine("Payment processed");
        
        // Fulfill order
        await orderGrain.FulfillOrderAsync();
        _outputHelper.WriteLine("Order fulfilled");
        
        // Ship order
        await orderGrain.ShipOrderAsync(new ShippingInfo
        {
            Carrier = "FedEx",
            TrackingNumber = $"TRACK-{Guid.NewGuid():N}"
        });
        
        _outputHelper.WriteLine("Order shipped");
        
        // Complete order
        await orderGrain.CompleteOrderAsync();
        
        var finalState = await orderGrain.GetOrderStateAsync();
        finalState.Should().Be(OrderState.Completed);
        
        // Get complete history
        var history = await orderGrain.GetOrderHistoryAsync();
        history.Should().NotBeEmpty();
        
        _outputHelper.WriteLine($"Order completed with {history.Count} state transitions");
        
        foreach (var entry in history)
        {
            _outputHelper.WriteLine($"  {entry.Timestamp:HH:mm:ss} - {entry.FromState} → {entry.ToState} ({entry.Trigger})");
        }
    }
}

// Test grain interfaces for comprehensive integration tests

public interface IComprehensiveWorkflowGrain : IGrainWithStringKey
{
    Task InitializeWithVersionAsync(StateMachineVersion version);
    Task StartWorkflowAsync(WorkflowData data);
    Task<WorkflowState> GetCurrentStateAsync();
    Task<List<StateTransitionEvent<WorkflowState, WorkflowTrigger>>> GetEventHistoryAsync();
    Task StartTimerAsync(string timerName, TimeSpan duration);
    Task<VersionUpgradeResult> UpgradeToVersionAsync(StateMachineVersion targetVersion, MigrationStrategy strategy);
    Task<SagaStepResult> ExecuteSagaStepAsync(string stepName, SagaContext context);
    Task<bool> IsInStateAsync(WorkflowState state);
    Task<List<WorkflowState>> GetDescendantStatesAsync(WorkflowState parentState);
    Task CompleteWorkflowAsync();
    Task<List<AuditEntry>> GetAuditTrailAsync();
    Task TriggerCompensationAsync();
    Task<List<CompensationExecution>> GetCompensationHistoryAsync();
}

public interface IPerformanceTestGrain : IGrainWithStringKey
{
    Task InitializeAsync();
    Task TransitionAsync();
}

public interface IResilientWorkflowGrain : IGrainWithStringKey
{
    Task InitializeAsync();
    Task ProcessStepAsync(string stepName);
    Task<string> GetStateAsync();
    Task<int> GetEventCountAsync();
    Task DeactivateAsync();
}

public interface IComplexSagaGrain : IGrainWithStringKey
{
    Task<SagaExecutionResult> ExecuteAsync(ComplexSagaData data, string correlationId);
}

public interface IIntrospectableWorkflowGrain : IGrainWithStringKey
{
    Task InitializeAsync();
    Task<OrleansStateMachineInfo> GetStateMachineInfoAsync();
    Task<EnhancedStateMachineConfiguration<WorkflowState, WorkflowTrigger>> GetDetailedConfigurationAsync();
    Task<string> GetDotGraphAsync();
    Task<StateMachineComparison<WorkflowState, WorkflowTrigger>> CompareWithVersionAsync(StateMachineVersion version);
}

public interface IOrderProcessingWorkflowGrain : IGrainWithStringKey
{
    Task CreateOrderAsync(OrderData data);
    Task ProcessPaymentAsync(PaymentInfo payment);
    Task FulfillOrderAsync();
    Task ShipOrderAsync(ShippingInfo shipping);
    Task CompleteOrderAsync();
    Task<OrderState> GetOrderStateAsync();
    Task<List<OrderHistoryEntry>> GetOrderHistoryAsync();
}

// Supporting data types

public class WorkflowData
{
    public string WorkflowId { get; set; } = "";
    public Dictionary<string, object> BusinessData { get; set; } = new();
}

public enum WorkflowState
{
    Idle, Active, Processing, Validating, Executing, Completed, Failed, Compensated
}

public enum WorkflowTrigger
{
    Start, Process, Validate, Execute, Complete, Fail, Compensate, Timeout
}

public class AuditEntry
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
}

public class ComplexSagaData
{
    public string SagaId { get; set; } = "";
    public List<string> Steps { get; set; } = new();
    public bool RequiresCompensation { get; set; }
}

public class SagaExecutionResult
{
    public bool IsSuccess { get; set; }
    public bool IsCompensated { get; set; }
    public string Status { get; set; } = "";
    public List<string> CompletedSteps { get; set; } = new();
    public List<string> CompensatedSteps { get; set; } = new();
}

public class OrderData
{
    public string OrderId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
}

public class OrderItem
{
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class PaymentInfo
{
    public string PaymentMethod { get; set; } = "";
    public string TransactionId { get; set; } = "";
}

public class ShippingInfo
{
    public string Carrier { get; set; } = "";
    public string TrackingNumber { get; set; } = "";
}

public enum OrderState
{
    Created, PaymentPending, PaymentProcessed, Fulfilling, Shipped, Completed, Cancelled
}

public class OrderHistoryEntry
{
    public DateTime Timestamp { get; set; }
    public OrderState FromState { get; set; }
    public OrderState ToState { get; set; }
    public string Trigger { get; set; } = "";
}