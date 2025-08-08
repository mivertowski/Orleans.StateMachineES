using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;

namespace Orleans.StateMachineES.Sagas.Advanced;

/// <summary>
/// Advanced saga orchestrator that supports parallel step execution and conditional branching.
/// Provides sophisticated workflow control with dependency management and dynamic routing.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public abstract class ParallelSagaOrchestrator<TSagaData> : Grain, IParallelSagaOrchestrator<TSagaData>
    where TSagaData : class
{
    private readonly SagaExecutionGraph<TSagaData> _executionGraph = new();
    private readonly ConcurrentDictionary<string, SagaStepExecutionContext<TSagaData>> _executionContexts = new();
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private ILogger<ParallelSagaOrchestrator<TSagaData>>? _logger;
    
    protected abstract void ConfigureWorkflow(ISagaWorkflowBuilder<TSagaData> builder);

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _logger = ServiceProvider.GetService(typeof(ILogger<ParallelSagaOrchestrator<TSagaData>>)) as ILogger<ParallelSagaOrchestrator<TSagaData>>;
        
        // Configure the workflow
        var builder = new SagaWorkflowBuilder<TSagaData>();
        ConfigureWorkflow(builder);
        _executionGraph.BuildFromConfiguration(builder.GetConfiguration());
    }

    public async Task<ParallelSagaResult> ExecuteAsync(TSagaData sagaData, string correlationId)
    {
        await _executionSemaphore.WaitAsync();
        
        try
        {
            _logger?.LogInformation("Starting parallel saga execution for correlation {CorrelationId}", correlationId);
            
            var context = new ParallelSagaExecutionContext<TSagaData>
            {
                SagaData = sagaData,
                CorrelationId = correlationId,
                StartedAt = DateTime.UtcNow
            };

            // Execute the workflow
            var result = await ExecuteWorkflowAsync(context);
            
            _logger?.LogInformation("Completed parallel saga execution with result {Status}", result.Status);
            return result;
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    public async Task<SagaStepResult> ExecuteStepAsync(string stepName, TSagaData sagaData, SagaContext context)
    {
        if (!_executionGraph.HasStep(stepName))
        {
            return SagaStepResult.TechnicalFailure($"Step '{stepName}' not found in execution graph", null);
        }

        var step = _executionGraph.GetStep(stepName);
        var executionContext = new SagaStepExecutionContext<TSagaData>
        {
            StepName = stepName,
            SagaData = sagaData,
            Context = context,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger?.LogDebug("Executing step {StepName}", stepName);
            
            var result = await step.ExecuteAsync(sagaData, context);
            
            executionContext.CompletedAt = DateTime.UtcNow;
            executionContext.Result = result;
            executionContext.Status = result.IsSuccess ? StepExecutionStatus.Succeeded : StepExecutionStatus.Failed;
            
            _executionContexts.TryAdd($"{context.CorrelationId}:{stepName}", executionContext);
            
            _logger?.LogDebug("Step {StepName} completed with status {Status}", stepName, executionContext.Status);
            return result;
        }
        catch (Exception ex)
        {
            executionContext.CompletedAt = DateTime.UtcNow;
            executionContext.Exception = ex;
            executionContext.Status = StepExecutionStatus.Failed;
            
            _logger?.LogError(ex, "Step {StepName} failed with exception", stepName);
            return SagaStepResult.TechnicalFailure(ex.Message, ex);
        }
    }

    public async Task<CompensationResult> CompensateAsync(TSagaData sagaData, string correlationId, Exception? error)
    {
        _logger?.LogWarning("Starting compensation for correlation {CorrelationId}", correlationId);
        
        var executedSteps = _executionContexts.Values
            .Where(ctx => ctx.Context.CorrelationId == correlationId && ctx.Status == StepExecutionStatus.Succeeded)
            .OrderByDescending(ctx => ctx.CompletedAt)
            .ToList();

        var compensationResults = new List<CompensationResult>();
        
        foreach (var executedStep in executedSteps)
        {
            try
            {
                var step = _executionGraph.GetStep(executedStep.StepName);
                var result = await step.CompensateAsync(sagaData, executedStep.Result, executedStep.Context);
                compensationResults.Add(result);
                
                _logger?.LogDebug("Compensated step {StepName} with result {Success}", 
                    executedStep.StepName, result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to compensate step {StepName}", executedStep.StepName);
                compensationResults.Add(CompensationResult.Failure($"Compensation failed: {ex.Message}"));
            }
        }

        var allSuccessful = compensationResults.All(r => r.IsSuccess);
        return allSuccessful 
            ? CompensationResult.Success()
            : CompensationResult.Failure($"Some compensations failed: {compensationResults.Count(r => r.IsSuccess)}/{compensationResults.Count} successful");
    }

    public Task<List<SagaStepExecutionInfo>> GetExecutionHistoryAsync(string correlationId)
    {
        var history = _executionContexts.Values
            .Where(ctx => ctx.Context.CorrelationId == correlationId)
            .Select(ctx => new SagaStepExecutionInfo
            {
                StepName = ctx.StepName,
                Status = ctx.Status,
                StartedAt = ctx.StartedAt,
                CompletedAt = ctx.CompletedAt,
                Duration = ctx.CompletedAt?.Subtract(ctx.StartedAt),
                IsSuccess = ctx.Status == StepExecutionStatus.Succeeded,
                ErrorMessage = ctx.Exception?.Message
            })
            .OrderBy(info => info.StartedAt)
            .ToList();

        return Task.FromResult(history);
    }

    private async Task<ParallelSagaResult> ExecuteWorkflowAsync(ParallelSagaExecutionContext<TSagaData> context)
    {
        var result = new ParallelSagaResult
        {
            CorrelationId = context.CorrelationId,
            StartedAt = context.StartedAt
        };

        try
        {
            // Get entry points (steps with no dependencies)
            var entrySteps = _executionGraph.GetEntrySteps();
            
            if (!entrySteps.Any())
            {
                result.Status = SagaStatus.Failed;
                result.ErrorMessage = "No entry steps found in execution graph";
                return result;
            }

            // Execute the workflow using topological sort and parallel execution
            var completedSteps = new HashSet<string>();
            var failedSteps = new HashSet<string>();
            var pendingSteps = new Queue<SagaWorkflowStep<TSagaData>>(entrySteps);

            while (pendingSteps.Count > 0)
            {
                var readySteps = new List<SagaWorkflowStep<TSagaData>>();
                var remainingSteps = new List<SagaWorkflowStep<TSagaData>>();

                // Find steps that are ready to execute (dependencies satisfied)
                while (pendingSteps.TryDequeue(out var step))
                {
                    if (step.Dependencies.All(dep => completedSteps.Contains(dep) || failedSteps.Contains(dep)))
                    {
                        // Check conditional execution
                        if (await EvaluateStepCondition(step, context, completedSteps, failedSteps))
                        {
                            readySteps.Add(step);
                        }
                        else
                        {
                            _logger?.LogDebug("Step {StepName} skipped due to condition evaluation", step.Name);
                            completedSteps.Add(step.Name); // Mark as completed but not executed
                        }
                    }
                    else
                    {
                        remainingSteps.Add(step);
                    }
                }

                // Re-queue steps that aren't ready yet
                foreach (var step in remainingSteps)
                {
                    pendingSteps.Enqueue(step);
                }

                if (!readySteps.Any())
                {
                    if (pendingSteps.Count > 0)
                    {
                        // Deadlock or all remaining steps have failed dependencies
                        result.Status = SagaStatus.Failed;
                        result.ErrorMessage = "Deadlock detected or unresolvable dependencies";
                        break;
                    }
                    
                    // No more steps to execute
                    break;
                }

                // Execute ready steps in parallel
                var executionTasks = readySteps.Select(step => 
                    ExecuteStepWithDependencyCheck(step, context, completedSteps, failedSteps));

                var stepResults = await Task.WhenAll(executionTasks);

                // Process results and update completed/failed sets
                for (int i = 0; i < readySteps.Count; i++)
                {
                    var step = readySteps[i];
                    var stepResult = stepResults[i];

                    if (stepResult.IsSuccess)
                    {
                        completedSteps.Add(step.Name);
                        result.SuccessfulSteps.Add(step.Name);
                        
                        // Add dependent steps to pending queue
                        var dependentSteps = _executionGraph.GetDependentSteps(step.Name);
                        foreach (var dependentStep in dependentSteps)
                        {
                            if (!completedSteps.Contains(dependentStep.Name) && !failedSteps.Contains(dependentStep.Name))
                            {
                                pendingSteps.Enqueue(dependentStep);
                            }
                        }
                    }
                    else
                    {
                        failedSteps.Add(step.Name);
                        result.FailedSteps.Add(step.Name);
                        
                        if (!step.ContinueOnFailure)
                        {
                            result.Status = SagaStatus.Failed;
                            result.ErrorMessage = $"Critical step '{step.Name}' failed: {stepResult.ErrorMessage}";
                            
                            // Trigger compensation
                            await CompensateAsync(context.SagaData, context.CorrelationId, 
                                stepResult.Exception ?? new Exception(stepResult.ErrorMessage ?? "Step failed"));
                            
                            return result;
                        }
                    }
                }
            }

            result.CompletedAt = DateTime.UtcNow;
            result.Status = failedSteps.Any() ? SagaStatus.Compensated : SagaStatus.Completed;
            result.TotalSteps = completedSteps.Count + failedSteps.Count;

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Workflow execution failed");
            
            result.Status = SagaStatus.Failed;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;

            await CompensateAsync(context.SagaData, context.CorrelationId, ex);
            
            return result;
        }
    }

    private async Task<SagaStepResult> ExecuteStepWithDependencyCheck(
        SagaWorkflowStep<TSagaData> step, 
        ParallelSagaExecutionContext<TSagaData> context,
        HashSet<string> completedSteps,
        HashSet<string> failedSteps)
    {
        // Verify all dependencies are still satisfied
        var unsatisfiedDependencies = step.Dependencies.Where(dep => 
            !completedSteps.Contains(dep) && !failedSteps.Contains(dep)).ToList();

        if (unsatisfiedDependencies.Any())
        {
            return SagaStepResult.TechnicalFailure(
                $"Dependencies not satisfied: {string.Join(", ", unsatisfiedDependencies)}", null);
        }

        // Check if any required dependencies failed
        var failedDependencies = step.Dependencies.Where(dep => failedSteps.Contains(dep)).ToList();
        if (failedDependencies.Any() && !step.ContinueOnFailure)
        {
            return SagaStepResult.TechnicalFailure(
                $"Required dependencies failed: {string.Join(", ", failedDependencies)}", null);
        }

        var sagaContext = new SagaContext
        {
            CorrelationId = context.CorrelationId,
            BusinessTransactionId = context.BusinessTransactionId,
            Metadata = new Dictionary<string, object>
            {
                ["CompletedSteps"] = completedSteps.ToList(),
                ["FailedSteps"] = failedSteps.ToList()
            }
        };

        return await ExecuteStepAsync(step.Name, context.SagaData, sagaContext);
    }

    private async Task<bool> EvaluateStepCondition(
        SagaWorkflowStep<TSagaData> step,
        ParallelSagaExecutionContext<TSagaData> context,
        HashSet<string> completedSteps,
        HashSet<string> failedSteps)
    {
        if (step.Condition == null)
            return true;

        var conditionContext = new SagaConditionContext<TSagaData>
        {
            SagaData = context.SagaData,
            CompletedSteps = completedSteps,
            FailedSteps = failedSteps,
            ExecutionContexts = _executionContexts.Values
                .Where(ctx => ctx.Context.CorrelationId == context.CorrelationId)
                .ToList()
        };

        try
        {
            return await step.Condition(conditionContext);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error evaluating condition for step {StepName}", step.Name);
            return false;
        }
    }
}

/// <summary>
/// Interface for parallel saga orchestrator with advanced workflow capabilities.
/// </summary>
public interface IParallelSagaOrchestrator<TSagaData> : IGrain
    where TSagaData : class
{
    Task<ParallelSagaResult> ExecuteAsync(TSagaData sagaData, string correlationId);
    Task<SagaStepResult> ExecuteStepAsync(string stepName, TSagaData sagaData, SagaContext context);
    Task<CompensationResult> CompensateAsync(TSagaData sagaData, string correlationId, Exception? error);
    Task<List<SagaStepExecutionInfo>> GetExecutionHistoryAsync(string correlationId);
}

/// <summary>
/// Result of parallel saga execution with detailed information.
/// </summary>
[GenerateSerializer]
public class ParallelSagaResult
{
    [Id(0)] public string CorrelationId { get; set; } = "";
    [Id(1)] public SagaStatus Status { get; set; }
    [Id(2)] public DateTime StartedAt { get; set; }
    [Id(3)] public DateTime? CompletedAt { get; set; }
    [Id(4)] public int TotalSteps { get; set; }
    [Id(5)] public List<string> SuccessfulSteps { get; set; } = new();
    [Id(6)] public List<string> FailedSteps { get; set; } = new();
    [Id(7)] public string? ErrorMessage { get; set; }
    
    public TimeSpan? Duration => CompletedAt?.Subtract(StartedAt);
    public bool IsSuccess => Status == SagaStatus.Completed;
    public bool IsPartialSuccess => Status == SagaStatus.Compensated;
    public bool IsFailed => Status == SagaStatus.Failed;
}

/// <summary>
/// Context for parallel saga execution.
/// </summary>
public class ParallelSagaExecutionContext<TSagaData>
    where TSagaData : class
{
    public TSagaData SagaData { get; set; } = default!;
    public string CorrelationId { get; set; } = "";
    public string BusinessTransactionId { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Context for step execution in parallel saga.
/// </summary>
public class SagaStepExecutionContext<TSagaData>
    where TSagaData : class
{
    public string StepName { get; set; } = "";
    public TSagaData SagaData { get; set; } = default!;
    public SagaContext Context { get; set; } = default!;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public StepExecutionStatus Status { get; set; }
    public SagaStepResult? Result { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Information about saga step execution.
/// </summary>
[GenerateSerializer]
public class SagaStepExecutionInfo
{
    [Id(0)] public string StepName { get; set; } = "";
    [Id(1)] public StepExecutionStatus Status { get; set; }
    [Id(2)] public DateTime StartedAt { get; set; }
    [Id(3)] public DateTime? CompletedAt { get; set; }
    [Id(4)] public TimeSpan? Duration { get; set; }
    [Id(5)] public bool IsSuccess { get; set; }
    [Id(6)] public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of step execution in parallel saga.
/// </summary>
public enum StepExecutionStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Compensated,
    Skipped
}

/// <summary>
/// Context for evaluating step conditions.
/// </summary>
public class SagaConditionContext<TSagaData>
    where TSagaData : class
{
    public TSagaData SagaData { get; set; } = default!;
    public HashSet<string> CompletedSteps { get; set; } = new();
    public HashSet<string> FailedSteps { get; set; } = new();
    public List<SagaStepExecutionContext<TSagaData>> ExecutionContexts { get; set; } = new();
    
    public bool IsStepCompleted(string stepName) => CompletedSteps.Contains(stepName);
    public bool IsStepFailed(string stepName) => FailedSteps.Contains(stepName);
    public SagaStepExecutionContext<TSagaData>? GetExecutionContext(string stepName) =>
        ExecutionContexts.FirstOrDefault(ctx => ctx.StepName == stepName);
}