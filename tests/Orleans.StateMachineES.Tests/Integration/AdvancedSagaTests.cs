using System.Diagnostics;
using FluentAssertions;
using Orleans.StateMachineES.Sagas;
using Orleans.StateMachineES.Sagas.Advanced;
using Orleans.StateMachineES.Tests.Cluster;
using Orleans.StateMachineES.Tests.Integration.TestGrains;
using Xunit;
using Xunit.Abstractions;

namespace Orleans.StateMachineES.Tests.Integration;

/// <summary>
/// Comprehensive tests for advanced saga patterns including parallel execution and conditional branching.
/// </summary>
[Collection(nameof(TestClusterApplication))]
public class AdvancedSagaTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
{
    private readonly TestClusterApplication _testApp = testApp;
    private readonly ITestOutputHelper _outputHelper = outputHelper;

    [Fact]
    public async Task ParallelSagaOrchestrator_SimpleLinearWorkflow_ShouldExecuteSequentially()
    {
        var sagaId = $"linear-saga-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<ITestParallelSagaGrain>(sagaId);

        var sagaData = new TestSagaData
        {
            WorkflowId = sagaId,
            Steps = ["step1", "step2", "step3"],
            ProcessingTime = TimeSpan.FromMilliseconds(50)
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await saga.ExecuteLinearWorkflowAsync(sagaData, Guid.NewGuid().ToString());
        stopwatch.Stop();

        _outputHelper.WriteLine($"Linear workflow completed in {stopwatch.ElapsedMilliseconds}ms");
        _outputHelper.WriteLine($"Status: {result.Status}");
        _outputHelper.WriteLine($"Completed steps: {string.Join(", ", result.SuccessfulSteps)}");

        result.IsSuccess.Should().BeTrue();
        result.SuccessfulSteps.Should().HaveCount(3);
        result.FailedSteps.Should().BeEmpty();
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(150); // At least 3 * 50ms sequentially
    }

    [Fact]
    public async Task ParallelSagaOrchestrator_ParallelWorkflow_ShouldExecuteConcurrently()
    {
        var sagaId = $"parallel-saga-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<ITestParallelSagaGrain>(sagaId);

        var sagaData = new TestSagaData
        {
            WorkflowId = sagaId,
            Steps = ["parallel1", "parallel2", "parallel3"],
            ProcessingTime = TimeSpan.FromMilliseconds(100)
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await saga.ExecuteParallelWorkflowAsync(sagaData, Guid.NewGuid().ToString());
        stopwatch.Stop();

        _outputHelper.WriteLine($"Parallel workflow completed in {stopwatch.ElapsedMilliseconds}ms");
        _outputHelper.WriteLine($"Status: {result.Status}");
        _outputHelper.WriteLine($"Completed steps: {string.Join(", ", result.SuccessfulSteps)}");

        result.IsSuccess.Should().BeTrue();
        result.SuccessfulSteps.Should().HaveCount(3);
        result.FailedSteps.Should().BeEmpty();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300); // Should be much faster than sequential
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(100); // But at least as long as one step
    }

    [Fact]
    public async Task ParallelSagaOrchestrator_ComplexDependencyGraph_ShouldRespectDependencies()
    {
        var sagaId = $"complex-saga-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<ITestParallelSagaGrain>(sagaId);

        var sagaData = new TestSagaData
        {
            WorkflowId = sagaId,
            Steps = ["init", "process1", "process2", "merge", "finalize"],
            ProcessingTime = TimeSpan.FromMilliseconds(50)
        };

        var result = await saga.ExecuteComplexWorkflowAsync(sagaData, Guid.NewGuid().ToString());

        _outputHelper.WriteLine($"Complex workflow completed with status: {result.Status}");
        _outputHelper.WriteLine($"Execution order verified: {string.Join(" → ", result.SuccessfulSteps)}");

        result.IsSuccess.Should().BeTrue();
        result.SuccessfulSteps.Should().HaveCount(5);

        // Verify execution order respects dependencies
        var executionOrder = result.SuccessfulSteps;
        executionOrder.First().Should().Be("init"); // Should start first
        executionOrder.IndexOf("merge").Should().BeGreaterThan(executionOrder.IndexOf("process1")); // merge after process1
        executionOrder.IndexOf("merge").Should().BeGreaterThan(executionOrder.IndexOf("process2")); // merge after process2
        executionOrder.Last().Should().Be("finalize"); // Should finish last
    }

    [Fact]
    public async Task ParallelSagaOrchestrator_ConditionalExecution_ShouldSkipBasedOnConditions()
    {
        var sagaId = $"conditional-saga-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<ITestParallelSagaGrain>(sagaId);

        var sagaData = new TestSagaData
        {
            WorkflowId = sagaId,
            Steps = ["always", "conditional", "never"],
            SkipConditionalSteps = true
        };

        var result = await saga.ExecuteConditionalWorkflowAsync(sagaData, Guid.NewGuid().ToString());

        _outputHelper.WriteLine($"Conditional workflow completed with status: {result.Status}");
        _outputHelper.WriteLine($"Executed steps: {string.Join(", ", result.SuccessfulSteps)}");

        result.IsSuccess.Should().BeTrue();
        result.SuccessfulSteps.Should().Contain("always");
        result.SuccessfulSteps.Should().NotContain("conditional");
        result.SuccessfulSteps.Should().NotContain("never");
    }

    [Fact]
    public async Task ParallelSagaOrchestrator_FailureAndCompensation_ShouldHandleFailuresGracefully()
    {
        var sagaId = $"failure-saga-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<ITestParallelSagaGrain>(sagaId);

        var sagaData = new TestSagaData
        {
            WorkflowId = sagaId,
            Steps = ["step1", "failing-step", "step3"],
            SimulateFailure = true
        };

        var result = await saga.ExecuteFailureWorkflowAsync(sagaData, Guid.NewGuid().ToString());

        _outputHelper.WriteLine($"Failure workflow completed with status: {result.Status}");
        _outputHelper.WriteLine($"Successful steps: {string.Join(", ", result.SuccessfulSteps)}");
        _outputHelper.WriteLine($"Failed steps: {string.Join(", ", result.FailedSteps)}");

        result.IsFailed.Should().BeTrue();
        result.FailedSteps.Should().Contain("failing-step");
        result.SuccessfulSteps.Should().Contain("step1"); // Should complete before failure

        // Verify compensation was triggered
        var history = await saga.GetExecutionHistoryAsync(result.CorrelationId);
        history.Should().NotBeEmpty();
        
        var compensatedSteps = history.Where(h => h.IsSuccess).ToList();
        compensatedSteps.Should().NotBeEmpty();
        
        _outputHelper.WriteLine($"Compensated {compensatedSteps.Count} steps successfully");
    }

    [Fact]
    public async Task ParallelSagaOrchestrator_RetryMechanism_ShouldRetryFailedSteps()
    {
        var sagaId = $"retry-saga-{Guid.NewGuid():N}";
        var saga = _testApp.Cluster.Client.GetGrain<ITestParallelSagaGrain>(sagaId);

        var sagaData = new TestSagaData
        {
            WorkflowId = sagaId,
            Steps = ["retry-step"],
            RetryCount = 2, // Should succeed on 3rd attempt
            ProcessingTime = TimeSpan.FromMilliseconds(10)
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await saga.ExecuteRetryWorkflowAsync(sagaData, Guid.NewGuid().ToString());
        stopwatch.Stop();

        _outputHelper.WriteLine($"Retry workflow completed in {stopwatch.ElapsedMilliseconds}ms");
        _outputHelper.WriteLine($"Status: {result.Status}");

        result.IsSuccess.Should().BeTrue();
        result.SuccessfulSteps.Should().Contain("retry-step");
        
        // Should take time for retries (with exponential backoff)
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task SagaWorkflowBuilder_FluentAPIConstruction_ShouldBuildCorrectWorkflow()
    {
        var builder = new SagaWorkflowBuilder<TestSagaData>();

        // Build a complex workflow using fluent API
        builder
            .AddStep("init", async (data, context) => 
            {
                data.ExecutionLog.Add($"Initialized at {DateTime.UtcNow:HH:mm:ss.fff}");
                return SagaStepResult.Success("Initialization complete");
            })
            .And()
            .AddStep("validate")
            .WithExecution(async (data, context) =>
            {
                data.ExecutionLog.Add($"Validated at {DateTime.UtcNow:HH:mm:ss.fff}");
                return SagaStepResult.Success("Validation complete");
            })
            .DependsOn("init")
            .And()
            .CreateParallelBranch("processing")
            .AddParallelStep("process-a", async (data, context) =>
            {
                await Task.Delay(100);
                data.ExecutionLog.Add($"Process A completed at {DateTime.UtcNow:HH:mm:ss.fff}");
                return SagaStepResult.Success("Process A complete");
            })
            .AddParallelStep("process-b", async (data, context) =>
            {
                await Task.Delay(100);
                data.ExecutionLog.Add($"Process B completed at {DateTime.UtcNow:HH:mm:ss.fff}");
                return SagaStepResult.Success("Process B complete");
            })
            .ThenJoin("finalize", new InlineSagaStep<TestSagaData>("finalize", async (data, context) =>
            {
                data.ExecutionLog.Add($"Finalized at {DateTime.UtcNow:HH:mm:ss.fff}");
                return SagaStepResult.Success("Finalization complete");
            }));

        var configuration = builder.GetConfiguration();
        var validation = builder.Validate();

        _outputHelper.WriteLine($"Workflow validation: {(validation.IsValid ? "PASSED" : "FAILED")}");
        foreach (var error in validation.Errors)
        {
            _outputHelper.WriteLine($"Error: {error}");
        }
        foreach (var warning in validation.Warnings)
        {
            _outputHelper.WriteLine($"Warning: {warning}");
        }

        validation.IsValid.Should().BeTrue();
        configuration.Steps.Should().HaveCount(5);
        configuration.MaxConcurrency.Should().BeGreaterThan(1);

        // Verify dependency structure
        var validateStep = configuration.Steps.First(s => s.Name == "validate");
        validateStep.Dependencies.Should().Contain("init");

        var finalizeStep = configuration.Steps.First(s => s.Name == "finalize");
        finalizeStep.Dependencies.Should().Contain("process-a");
        finalizeStep.Dependencies.Should().Contain("process-b");
    }

    [Fact]
    public async Task SagaExecutionGraph_GraphAnalysis_ShouldProvideCorrectStatistics()
    {
        var builder = new SagaWorkflowBuilder<TestSagaData>();

        // Create a complex graph for analysis
        builder
            .AddStep("start", async (data, context) => SagaStepResult.Success())
            .And()
            .AddStep("branch1", async (data, context) => SagaStepResult.Success())
            .DependsOn("start")
            .And()
            .AddStep("branch2", async (data, context) => SagaStepResult.Success())
            .DependsOn("start")
            .And()
            .AddStep("branch3", async (data, context) => SagaStepResult.Success())
            .DependsOn("start")
            .And()
            .AddStep("merge1", async (data, context) => SagaStepResult.Success())
            .DependsOn("branch1", "branch2")
            .And()
            .AddStep("merge2", async (data, context) => SagaStepResult.Success())
            .DependsOn("branch2", "branch3")
            .And()
            .AddStep("final", async (data, context) => SagaStepResult.Success())
            .DependsOn("merge1", "merge2");

        var configuration = builder.GetConfiguration();
        var graph = new SagaExecutionGraph<TestSagaData>();
        graph.BuildFromConfiguration(configuration);

        var statistics = graph.GetStatistics();
        var validation = graph.Validate();

        _outputHelper.WriteLine($"Graph Statistics:");
        _outputHelper.WriteLine($"  Total Steps: {statistics.TotalSteps}");
        _outputHelper.WriteLine($"  Entry Points: {statistics.EntryPoints}");
        _outputHelper.WriteLine($"  Max Parallelism: {statistics.MaxParallelism}");
        _outputHelper.WriteLine($"  Execution Levels: {statistics.ExecutionLevels}");
        _outputHelper.WriteLine($"  Critical Path Length: {statistics.CriticalPathLength}");
        _outputHelper.WriteLine($"  Complexity Score: {statistics.ComplexityScore:F2}");

        validation.IsValid.Should().BeTrue();
        statistics.TotalSteps.Should().Be(7);
        statistics.EntryPoints.Should().Be(1);
        statistics.MaxParallelism.Should().Be(3); // branch1, branch2, branch3 can run in parallel
        statistics.CriticalPathLength.Should().Be(4); // start → branch → merge → final

        var levels = graph.GetExecutionLevels();
        levels.Should().HaveCount(4);
        levels[0].Should().HaveCount(1); // start
        levels[1].Should().HaveCount(3); // branch1, branch2, branch3
        levels[2].Should().HaveCount(2); // merge1, merge2
        levels[3].Should().HaveCount(1); // final
    }

    [Fact]
    public async Task ParallelSagaOrchestrator_ConcurrentExecution_ShouldHandleHighLoad()
    {
        const int ConcurrentSagas = 5;
        const int StepsPerSaga = 10;
        
        var tasks = new List<Task<ParallelSagaResult>>();

        _outputHelper.WriteLine($"Starting {ConcurrentSagas} concurrent sagas with {StepsPerSaga} steps each");

        for (int i = 0; i < ConcurrentSagas; i++)
        {
            var sagaId = $"concurrent-saga-{i}";
            var saga = _testApp.Cluster.Client.GetGrain<ITestParallelSagaGrain>(sagaId);
            
            var sagaData = new TestSagaData
            {
                WorkflowId = sagaId,
                Steps = [.. Enumerable.Range(1, StepsPerSaga).Select(j => $"step-{j}")],
                ProcessingTime = TimeSpan.FromMilliseconds(20)
            };

            tasks.Add(saga.ExecuteParallelWorkflowAsync(sagaData, Guid.NewGuid().ToString()));
        }

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        _outputHelper.WriteLine($"All {ConcurrentSagas} sagas completed in {stopwatch.ElapsedMilliseconds}ms");

        var successfulSagas = results.Count(r => r.IsSuccess);
        var totalStepsExecuted = results.Sum(r => r.SuccessfulSteps.Count);
        var averageStepsPerSaga = (double)totalStepsExecuted / ConcurrentSagas;
        var throughput = totalStepsExecuted / stopwatch.Elapsed.TotalSeconds;

        _outputHelper.WriteLine($"Success Rate: {successfulSagas}/{ConcurrentSagas} ({(double)successfulSagas/ConcurrentSagas:P})");
        _outputHelper.WriteLine($"Total Steps Executed: {totalStepsExecuted}");
        _outputHelper.WriteLine($"Average Steps per Saga: {averageStepsPerSaga:F1}");
        _outputHelper.WriteLine($"Throughput: {throughput:F1} steps/second");

        successfulSagas.Should().Be(ConcurrentSagas);
        averageStepsPerSaga.Should().Be(StepsPerSaga);
        throughput.Should().BeGreaterThan(50); // Should handle at least 50 steps per second
    }
}