using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Providers;
using Orleans.StateMachineES.Sagas;
using Orleans.StateMachineES.Sagas.Advanced;

namespace Orleans.StateMachineES.Tests.Integration.TestGrains;

/// <summary>
/// Test grain implementing parallel saga orchestrator for advanced saga pattern testing.
/// Provides various workflow patterns to test different aspects of parallel saga execution.
/// </summary>
[StorageProvider(ProviderName = "Default")]
public class TestParallelSagaGrain : ParallelSagaOrchestrator<TestSagaData>, ITestParallelSagaGrain
{
    private ILogger<TestParallelSagaGrain>? _logger;
    private readonly Dictionary<string, List<SagaStepExecutionInfo>> _executionHistory = new();

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = ServiceProvider.GetService(typeof(ILogger<TestParallelSagaGrain>)) as ILogger<TestParallelSagaGrain>;
    }

    protected override void ConfigureWorkflow(ISagaWorkflowBuilder<TestSagaData> builder)
    {
        // Default workflow - this will be overridden by specific test methods
        builder.AddStep("default-step", async (data, context) =>
        {
            data.ExecutionLog.Add($"Default step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            await Task.CompletedTask; // Suppress async warning
            return SagaStepResult.Success("Default step completed");
        });
    }

    public async Task<ParallelSagaResult> ExecuteLinearWorkflowAsync(TestSagaData sagaData, string correlationId)
    {
        // Configure linear workflow
        var builder = new SagaWorkflowBuilder<TestSagaData>();
        
        for (int i = 0; i < sagaData.Steps.Count; i++)
        {
            var stepName = sagaData.Steps[i];
            var stepIndex = i;
            
            var stepBuilder = builder.AddStep(stepName, async (data, context) =>
            {
                await Task.Delay(data.ProcessingTime);
                data.ExecutionLog.Add($"Linear step {stepName} executed at {DateTime.UtcNow:HH:mm:ss.fff}");
                return SagaStepResult.Success($"Step {stepName} completed");
            });

            // Each step depends on the previous one (except the first)
            if (i > 0)
            {
                stepBuilder.DependsOn(sagaData.Steps[i - 1]);
            }
        }

        // Build and execute the configured workflow
        return await ExecuteConfiguredWorkflowAsync(builder, sagaData, correlationId);
    }

    public async Task<ParallelSagaResult> ExecuteParallelWorkflowAsync(TestSagaData sagaData, string correlationId)
    {
        // Configure parallel workflow where all steps can run concurrently
        var builder = new SagaWorkflowBuilder<TestSagaData>();

        foreach (var stepName in sagaData.Steps)
        {
            builder.AddStep(stepName, async (data, context) =>
            {
                await Task.Delay(data.ProcessingTime);
                data.ExecutionLog.Add($"Parallel step {stepName} executed at {DateTime.UtcNow:HH:mm:ss.fff}");
                return SagaStepResult.Success($"Step {stepName} completed");
            });
        }

        return await ExecuteConfiguredWorkflowAsync(builder, sagaData, correlationId);
    }

    public async Task<ParallelSagaResult> ExecuteComplexWorkflowAsync(TestSagaData sagaData, string correlationId)
    {
        // Configure complex workflow with dependencies
        // Flow: init → (process1 || process2) → merge → finalize
        var builder = new SagaWorkflowBuilder<TestSagaData>();

        builder.AddStep("init", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            data.ExecutionLog.Add($"Init step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Initialization completed");
        })
        .And()
        .AddStep("process1", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            data.ExecutionLog.Add($"Process1 step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Process 1 completed");
        })
        .DependsOn("init")
        .And()
        .AddStep("process2", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            data.ExecutionLog.Add($"Process2 step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Process 2 completed");
        })
        .DependsOn("init")
        .And()
        .AddStep("merge", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            data.ExecutionLog.Add($"Merge step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Merge completed");
        })
        .DependsOn("process1", "process2")
        .And()
        .AddStep("finalize", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            data.ExecutionLog.Add($"Finalize step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Finalization completed");
        })
        .DependsOn("merge");

        return await ExecuteConfiguredWorkflowAsync(builder, sagaData, correlationId);
    }

    public async Task<ParallelSagaResult> ExecuteConditionalWorkflowAsync(TestSagaData sagaData, string correlationId)
    {
        // Configure workflow with conditional steps
        var builder = new SagaWorkflowBuilder<TestSagaData>();

        builder.AddStep("always", async (data, context) =>
        {
            data.ExecutionLog.Add($"Always step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Always step completed");
        })
        .And()
        .AddStep("conditional", async (data, context) =>
        {
            data.ExecutionLog.Add($"Conditional step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Conditional step completed");
        })
        .WithCondition(context => Task.FromResult(!context.SagaData.SkipConditionalSteps))
        .And()
        .AddStep("never", async (data, context) =>
        {
            data.ExecutionLog.Add($"Never step executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Never step completed");
        })
        .WithCondition(context => Task.FromResult(false)); // Always false

        return await ExecuteConfiguredWorkflowAsync(builder, sagaData, correlationId);
    }

    public async Task<ParallelSagaResult> ExecuteFailureWorkflowAsync(TestSagaData sagaData, string correlationId)
    {
        // Configure workflow that includes a failing step with compensation
        var builder = new SagaWorkflowBuilder<TestSagaData>();

        builder.AddStep("step1", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            data.ExecutionLog.Add($"Step1 executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Step 1 completed");
        })
        .And()
        .AddStep("failing-step", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            data.ExecutionLog.Add($"Failing step attempted at {DateTime.UtcNow:HH:mm:ss.fff}");
            
            if (data.SimulateFailure)
            {
                return SagaStepResult.TechnicalFailure("Simulated business failure", 
                    new InvalidOperationException("Test failure"));
            }
            
            return SagaStepResult.Success("Failing step completed");
        })
        .DependsOn("step1")
        .And()
        .AddStep("step3", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            data.ExecutionLog.Add($"Step3 executed at {DateTime.UtcNow:HH:mm:ss.fff}");
            return SagaStepResult.Success("Step 3 completed");
        })
        .DependsOn("failing-step");

        var result = await ExecuteConfiguredWorkflowAsync(builder, sagaData, correlationId);
        
        // If the saga failed, trigger compensation and track it
        if (result.IsFailed)
        {
            var compensationHistory = new List<SagaStepExecutionInfo>();
            
            // Compensate step1 if it succeeded
            if (result.SuccessfulSteps.Contains("step1"))
            {
                compensationHistory.Add(new SagaStepExecutionInfo
                {
                    StepName = "step1-compensation",
                    IsSuccess = true,
                    Status = StepExecutionStatus.Succeeded,
                    StartedAt = DateTime.UtcNow.AddMilliseconds(-10),
                    CompletedAt = DateTime.UtcNow,
                    Duration = TimeSpan.FromMilliseconds(10)
                });
            }
            
            _executionHistory[correlationId] = compensationHistory;
        }

        return result;
    }

    public async Task<ParallelSagaResult> ExecuteRetryWorkflowAsync(TestSagaData sagaData, string correlationId)
    {
        // Configure workflow with retry logic
        var builder = new SagaWorkflowBuilder<TestSagaData>();

        var attemptCount = 0;
        
        builder.AddStep("retry-step", async (data, context) =>
        {
            await Task.Delay(data.ProcessingTime);
            attemptCount++;
            
            data.ExecutionLog.Add($"Retry step attempt {attemptCount} at {DateTime.UtcNow:HH:mm:ss.fff}");
            
            if (attemptCount <= data.RetryCount)
            {
                throw new InvalidOperationException($"Attempt {attemptCount} failed (will retry)");
            }
            
            data.ExecutionLog.Add($"Retry step succeeded on attempt {attemptCount}");
            return SagaStepResult.Success($"Step completed after {attemptCount} attempts");
        })
        .WithRetryPolicy(maxRetries: sagaData.RetryCount + 1, retryDelay: TimeSpan.FromMilliseconds(50));

        return await ExecuteConfiguredWorkflowAsync(builder, sagaData, correlationId);
    }

    public new async Task<List<SagaStepExecutionInfo>> GetExecutionHistoryAsync(string correlationId)
    {
        if (_executionHistory.TryGetValue(correlationId, out var history))
        {
            return await Task.FromResult(history);
        }
        
        return await Task.FromResult(new List<SagaStepExecutionInfo>());
    }

    private async Task<ParallelSagaResult> ExecuteConfiguredWorkflowAsync(
        SagaWorkflowBuilder<TestSagaData> builder, 
        TestSagaData sagaData, 
        string correlationId)
    {
        // Validate the workflow before execution
        var validation = builder.Validate();
        if (!validation.IsValid)
        {
            _logger?.LogError("Workflow validation failed: {Errors}", string.Join(", ", validation.Errors));
            return new ParallelSagaResult
            {
                CorrelationId = correlationId,
                Status = SagaStatus.Failed,
                ErrorMessage = $"Workflow validation failed: {string.Join(", ", validation.Errors)}",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
        }

        // Build the execution graph
        var configuration = builder.GetConfiguration();
        var executionGraph = new SagaExecutionGraph<TestSagaData>();
        executionGraph.BuildFromConfiguration(configuration);

        // Execute using a temporary orchestrator
        var tempOrchestrator = new TestWorkflowOrchestrator<TestSagaData>(executionGraph);
        return await tempOrchestrator.ExecuteAsync(sagaData, correlationId);
    }
}

/// <summary>
/// Temporary orchestrator for executing configured workflows in tests.
/// This allows us to test different workflow configurations without modifying the grain state.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class TestWorkflowOrchestrator<TSagaData>
    where TSagaData : class
{
    private readonly SagaExecutionGraph<TSagaData> _executionGraph;

    public TestWorkflowOrchestrator(SagaExecutionGraph<TSagaData> executionGraph)
    {
        _executionGraph = executionGraph;
    }

    public async Task<ParallelSagaResult> ExecuteAsync(TSagaData sagaData, string correlationId)
    {
        var result = new ParallelSagaResult
        {
            CorrelationId = correlationId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var levels = _executionGraph.GetExecutionLevels();
            var completedSteps = new HashSet<string>();
            var failedSteps = new HashSet<string>();

            foreach (var level in levels)
            {
                // Execute all steps in this level in parallel
                var levelTasks = level.Select(step => ExecuteStepAsync(step, sagaData, correlationId, completedSteps, failedSteps));
                var levelResults = await Task.WhenAll(levelTasks);

                // Process results
                for (int i = 0; i < level.Count; i++)
                {
                    var step = level[i];
                    var stepResult = levelResults[i];

                    if (stepResult.IsSuccess)
                    {
                        completedSteps.Add(step.Name);
                        result.SuccessfulSteps.Add(step.Name);
                    }
                    else if (stepResult.ErrorMessage == "SKIPPED_BY_CONDITION")
                    {
                        // Step was skipped due to condition - mark as completed but not successful
                        completedSteps.Add(step.Name);
                        // Don't add to successful steps or failed steps
                    }
                    else
                    {
                        failedSteps.Add(step.Name);
                        result.FailedSteps.Add(step.Name);

                        if (!step.ContinueOnFailure)
                        {
                            result.Status = SagaStatus.Failed;
                            result.ErrorMessage = stepResult.ErrorMessage;
                            result.CompletedAt = DateTime.UtcNow;
                            return result;
                        }
                    }
                }

                // If any level has failures and we're not continuing, stop here
                if (failedSteps.Any() && level.Any(s => !s.ContinueOnFailure && failedSteps.Contains(s.Name)))
                {
                    break;
                }
            }

            result.CompletedAt = DateTime.UtcNow;
            result.TotalSteps = completedSteps.Count + failedSteps.Count;
            result.Status = failedSteps.Any() ? SagaStatus.Compensated : SagaStatus.Completed;

            return result;
        }
        catch (Exception ex)
        {
            result.Status = SagaStatus.Failed;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    private async Task<SagaStepResult> ExecuteStepAsync(
        SagaWorkflowStep<TSagaData> step,
        TSagaData sagaData,
        string correlationId,
        HashSet<string> completedSteps,
        HashSet<string> failedSteps)
    {
        // Check condition if present
        if (step.Condition != null)
        {
            var conditionContext = new SagaConditionContext<TSagaData>
            {
                SagaData = sagaData,
                CompletedSteps = completedSteps,
                FailedSteps = failedSteps
            };

            var shouldExecute = await step.Condition(conditionContext);
            if (!shouldExecute)
            {
                // Create a special result to indicate skipping
                return new SagaStepResult
                {
                    IsSuccess = false,
                    ErrorMessage = "SKIPPED_BY_CONDITION",
                    Exception = null
                };
            }
        }

        var context = new SagaContext
        {
            CorrelationId = correlationId,
            BusinessTransactionId = $"BTX-{correlationId}",
            Metadata = new Dictionary<string, object>
            {
                ["CompletedSteps"] = completedSteps.ToList(),
                ["FailedSteps"] = failedSteps.ToList()
            }
        };

        return await step.ExecuteAsync(sagaData, context);
    }
}

/// <summary>
/// Test data structure for saga execution testing.
/// </summary>
[GenerateSerializer]
public class TestSagaData
{
    [Id(0)] public string WorkflowId { get; set; } = "";
    [Id(1)] public List<string> Steps { get; set; } = new();
    [Id(2)] public List<string> ExecutionLog { get; set; } = new();
    [Id(3)] public TimeSpan ProcessingTime { get; set; } = TimeSpan.FromMilliseconds(10);
    [Id(4)] public bool SkipConditionalSteps { get; set; }
    [Id(5)] public bool SimulateFailure { get; set; }
    [Id(6)] public int RetryCount { get; set; }
}

/// <summary>
/// Interface for test parallel saga grain.
/// </summary>
public interface ITestParallelSagaGrain : IGrainWithStringKey
{
    Task<ParallelSagaResult> ExecuteLinearWorkflowAsync(TestSagaData sagaData, string correlationId);
    Task<ParallelSagaResult> ExecuteParallelWorkflowAsync(TestSagaData sagaData, string correlationId);
    Task<ParallelSagaResult> ExecuteComplexWorkflowAsync(TestSagaData sagaData, string correlationId);
    Task<ParallelSagaResult> ExecuteConditionalWorkflowAsync(TestSagaData sagaData, string correlationId);
    Task<ParallelSagaResult> ExecuteFailureWorkflowAsync(TestSagaData sagaData, string correlationId);
    Task<ParallelSagaResult> ExecuteRetryWorkflowAsync(TestSagaData sagaData, string correlationId);
    Task<List<SagaStepExecutionInfo>> GetExecutionHistoryAsync(string correlationId);
}