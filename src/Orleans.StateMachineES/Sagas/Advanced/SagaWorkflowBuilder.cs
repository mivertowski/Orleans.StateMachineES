using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Orleans.StateMachineES.Sagas.Advanced;

/// <summary>
/// Builder pattern implementation for constructing complex saga workflows with dependencies and conditions.
/// Provides fluent API for defining workflow structure, dependencies, and execution logic.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class SagaWorkflowBuilder<TSagaData> : ISagaWorkflowBuilder<TSagaData>
    where TSagaData : class
{
    private readonly List<SagaWorkflowStep<TSagaData>> _steps = new();
    private readonly Dictionary<string, SagaStepBuilder<TSagaData>> _stepBuilders = new();

    /// <summary>
    /// Adds a new step to the workflow.
    /// </summary>
    public ISagaStepBuilder<TSagaData> AddStep(string stepName)
    {
        if (_stepBuilders.ContainsKey(stepName))
        {
            throw new ArgumentException($"Step '{stepName}' already exists in the workflow", nameof(stepName));
        }

        var stepBuilder = new SagaStepBuilder<TSagaData>(stepName, this);
        _stepBuilders[stepName] = stepBuilder;
        return stepBuilder;
    }

    /// <summary>
    /// Adds a step with implementation directly.
    /// </summary>
    public ISagaStepBuilder<TSagaData> AddStep(string stepName, ISagaStep<TSagaData> implementation)
    {
        var builder = AddStep(stepName);
        builder.WithImplementation(implementation);
        return builder;
    }

    /// <summary>
    /// Adds a step with inline execution logic.
    /// </summary>
    public ISagaStepBuilder<TSagaData> AddStep(string stepName, 
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null)
    {
        var implementation = new InlineSagaStep<TSagaData>(stepName, executeFunc, compensateFunc);
        return AddStep(stepName, implementation);
    }

    /// <summary>
    /// Creates a parallel branch where steps can execute concurrently.
    /// </summary>
    public IParallelBranchBuilder<TSagaData> CreateParallelBranch(string branchName)
    {
        return new ParallelBranchBuilder<TSagaData>(branchName, this);
    }

    /// <summary>
    /// Creates a conditional branch based on saga data.
    /// </summary>
    public IConditionalBranchBuilder<TSagaData> CreateConditionalBranch(
        Func<TSagaData, Task<bool>> condition)
    {
        return new ConditionalBranchBuilder<TSagaData>(condition, this);
    }

    /// <summary>
    /// Validates the workflow configuration.
    /// </summary>
    public SagaWorkflowValidationResult Validate()
    {
        var configuration = GetConfiguration();
        var graph = new SagaExecutionGraph<TSagaData>();
        graph.BuildFromConfiguration(configuration);
        
        var graphValidation = graph.Validate();
        var result = new SagaWorkflowValidationResult
        {
            IsValid = graphValidation.IsValid,
            Errors = graphValidation.Errors.ToList(),
            Warnings = graphValidation.Warnings.ToList()
        };

        // Additional workflow-specific validations
        ValidateStepImplementations(result);
        ValidateNamingConventions(result);
        ValidateComplexityLimits(result);

        return result;
    }

    /// <summary>
    /// Gets the final workflow configuration.
    /// </summary>
    public SagaWorkflowConfiguration<TSagaData> GetConfiguration()
    {
        var steps = _stepBuilders.Values.Select(builder => builder.Build()).ToList();
        
        return new SagaWorkflowConfiguration<TSagaData>
        {
            Steps = steps,
            Name = $"Workflow_{typeof(TSagaData).Name}",
            Description = $"Auto-generated workflow for {typeof(TSagaData).Name}",
            Version = "1.0.0",
            MaxConcurrency = CalculateMaxConcurrency(steps),
            TimeoutSettings = new WorkflowTimeoutSettings
            {
                StepTimeout = TimeSpan.FromMinutes(5),
                OverallTimeout = TimeSpan.FromHours(1)
            }
        };
    }

    internal void AddBuiltStep(SagaWorkflowStep<TSagaData> step)
    {
        _steps.Add(step);
    }

    private void ValidateStepImplementations(SagaWorkflowValidationResult result)
    {
        foreach (var builder in _stepBuilders.Values)
        {
            if (builder.Implementation == null)
            {
                result.Errors.Add($"Step '{builder.StepName}' has no implementation defined");
                result.IsValid = false;
            }
        }
    }

    private void ValidateNamingConventions(SagaWorkflowValidationResult result)
    {
        foreach (var stepName in _stepBuilders.Keys)
        {
            if (string.IsNullOrWhiteSpace(stepName))
            {
                result.Errors.Add("Step name cannot be null or whitespace");
                result.IsValid = false;
            }
            else if (stepName.Length > 50)
            {
                result.Warnings.Add($"Step name '{stepName}' is very long (>{50} characters)");
            }
        }

        // Check for duplicate names (case insensitive)
        var duplicates = _stepBuilders.Keys
            .GroupBy(name => name.ToLowerInvariant())
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicate in duplicates)
        {
            result.Errors.Add($"Duplicate step name detected (case insensitive): '{duplicate}'");
            result.IsValid = false;
        }
    }

    private void ValidateComplexityLimits(SagaWorkflowValidationResult result)
    {
        const int MaxSteps = 100;
        const int MaxDependenciesPerStep = 10;

        if (_stepBuilders.Count > MaxSteps)
        {
            result.Warnings.Add($"Workflow has {_stepBuilders.Count} steps, which exceeds recommended limit of {MaxSteps}");
        }

        foreach (var builder in _stepBuilders.Values)
        {
            if (builder.Dependencies.Count > MaxDependenciesPerStep)
            {
                result.Warnings.Add($"Step '{builder.StepName}' has {builder.Dependencies.Count} dependencies, which exceeds recommended limit of {MaxDependenciesPerStep}");
            }
        }
    }

    private int CalculateMaxConcurrency(List<SagaWorkflowStep<TSagaData>> steps)
    {
        if (!steps.Any()) return 1;

        var graph = new SagaExecutionGraph<TSagaData>();
        var config = new SagaWorkflowConfiguration<TSagaData> { Steps = steps };
        graph.BuildFromConfiguration(config);

        var statistics = graph.GetStatistics();
        return Math.Max(1, statistics.MaxParallelism);
    }
}

/// <summary>
/// Builder for individual saga steps.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class SagaStepBuilder<TSagaData> : ISagaStepBuilder<TSagaData>
    where TSagaData : class
{
    private readonly SagaWorkflowBuilder<TSagaData> _workflowBuilder;
    internal string StepName { get; }
    internal List<string> Dependencies { get; } = new();
    internal ISagaStep<TSagaData>? Implementation { get; private set; }
    internal bool ContinueOnFailureValue { get; private set; }
    internal Func<SagaConditionContext<TSagaData>, Task<bool>>? Condition { get; private set; }
    internal int MaxRetries { get; private set; } = 3;
    internal TimeSpan RetryDelay { get; private set; } = TimeSpan.FromSeconds(1);
    internal Dictionary<string, object> Metadata { get; } = new();

    public SagaStepBuilder(string stepName, SagaWorkflowBuilder<TSagaData> workflowBuilder)
    {
        StepName = stepName;
        _workflowBuilder = workflowBuilder;
    }

    public ISagaStepBuilder<TSagaData> DependsOn(params string[] stepNames)
    {
        Dependencies.AddRange(stepNames);
        return this;
    }

    public ISagaStepBuilder<TSagaData> WithImplementation(ISagaStep<TSagaData> implementation)
    {
        Implementation = implementation;
        return this;
    }

    public ISagaStepBuilder<TSagaData> WithExecution(
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null)
    {
        Implementation = new InlineSagaStep<TSagaData>(StepName, executeFunc, compensateFunc);
        return this;
    }

    public ISagaStepBuilder<TSagaData> ContinueOnFailure(bool continueOnFailure = true)
    {
        ContinueOnFailureValue = continueOnFailure;
        return this;
    }

    public ISagaStepBuilder<TSagaData> WithCondition(Func<SagaConditionContext<TSagaData>, Task<bool>> condition)
    {
        Condition = condition;
        return this;
    }

    public ISagaStepBuilder<TSagaData> WithRetryPolicy(int maxRetries, TimeSpan retryDelay)
    {
        MaxRetries = maxRetries;
        RetryDelay = retryDelay;
        return this;
    }

    public ISagaStepBuilder<TSagaData> WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    public ISagaWorkflowBuilder<TSagaData> And()
    {
        return _workflowBuilder;
    }

    internal SagaWorkflowStep<TSagaData> Build()
    {
        if (Implementation == null)
        {
            throw new InvalidOperationException($"Step '{StepName}' must have an implementation");
        }

        return new SagaWorkflowStep<TSagaData>
        {
            Name = StepName,
            Dependencies = Dependencies.ToList(),
            Implementation = Implementation,
            ContinueOnFailure = ContinueOnFailureValue,
            Condition = Condition,
            MaxRetries = MaxRetries,
            RetryDelay = RetryDelay,
            Metadata = new Dictionary<string, object>(Metadata)
        };
    }
}

/// <summary>
/// Builder for parallel execution branches.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class ParallelBranchBuilder<TSagaData> : IParallelBranchBuilder<TSagaData>
    where TSagaData : class
{
    private readonly string _branchName;
    private readonly SagaWorkflowBuilder<TSagaData> _workflowBuilder;
    private readonly List<string> _parallelSteps = new();

    public ParallelBranchBuilder(string branchName, SagaWorkflowBuilder<TSagaData> workflowBuilder)
    {
        _branchName = branchName;
        _workflowBuilder = workflowBuilder;
    }

    public IParallelBranchBuilder<TSagaData> AddParallelStep(string stepName, ISagaStep<TSagaData> implementation)
    {
        _workflowBuilder.AddStep(stepName, implementation);
        _parallelSteps.Add(stepName);
        return this;
    }

    public IParallelBranchBuilder<TSagaData> AddParallelStep(string stepName,
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null)
    {
        _workflowBuilder.AddStep(stepName, executeFunc, compensateFunc);
        _parallelSteps.Add(stepName);
        return this;
    }

    public ISagaWorkflowBuilder<TSagaData> ThenJoin(string joinStepName, ISagaStep<TSagaData> joinImplementation)
    {
        _workflowBuilder.AddStep(joinStepName, joinImplementation)
            .DependsOn(_parallelSteps.ToArray());
        return _workflowBuilder;
    }
}

/// <summary>
/// Builder for conditional execution branches.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class ConditionalBranchBuilder<TSagaData> : IConditionalBranchBuilder<TSagaData>
    where TSagaData : class
{
    private readonly Func<TSagaData, Task<bool>> _condition;
    private readonly SagaWorkflowBuilder<TSagaData> _workflowBuilder;

    public ConditionalBranchBuilder(Func<TSagaData, Task<bool>> condition, SagaWorkflowBuilder<TSagaData> workflowBuilder)
    {
        _condition = condition;
        _workflowBuilder = workflowBuilder;
    }

    public ISagaWorkflowBuilder<TSagaData> Then(string stepName, ISagaStep<TSagaData> implementation)
    {
        _workflowBuilder.AddStep(stepName, implementation)
            .WithCondition(async context => await _condition(context.SagaData));
        return _workflowBuilder;
    }

    public ISagaWorkflowBuilder<TSagaData> Then(string stepName,
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null)
    {
        _workflowBuilder.AddStep(stepName, executeFunc, compensateFunc)
            .WithCondition(async context => await _condition(context.SagaData));
        return _workflowBuilder;
    }
}

/// <summary>
/// Inline implementation of saga step for simple scenarios.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class InlineSagaStep<TSagaData> : ISagaStep<TSagaData>
    where TSagaData : class
{
    private readonly Func<TSagaData, SagaContext, Task<SagaStepResult>> _executeFunc;
    private readonly Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? _compensateFunc;

    public InlineSagaStep(
        string stepName,
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null,
        TimeSpan timeout = default,
        bool canRetry = true,
        int maxRetryAttempts = 3)
    {
        StepName = stepName;
        _executeFunc = executeFunc;
        _compensateFunc = compensateFunc;
        Timeout = timeout == default ? TimeSpan.FromMinutes(5) : timeout;
        CanRetry = canRetry;
        MaxRetryAttempts = maxRetryAttempts;
    }

    public string StepName { get; }
    public TimeSpan Timeout { get; }
    public bool CanRetry { get; }
    public int MaxRetryAttempts { get; }

    public async Task<SagaStepResult> ExecuteAsync(TSagaData sagaData, SagaContext context)
    {
        return await _executeFunc(sagaData, context);
    }

    public async Task<CompensationResult> CompensateAsync(TSagaData sagaData, SagaStepResult? originalResult, SagaContext context)
    {
        if (_compensateFunc != null)
        {
            return await _compensateFunc(sagaData, originalResult, context);
        }

        return CompensationResult.Success();
    }
}

/// <summary>
/// Configuration for a complete saga workflow.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class SagaWorkflowConfiguration<TSagaData>
    where TSagaData : class
{
    public List<SagaWorkflowStep<TSagaData>> Steps { get; set; } = new();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public int MaxConcurrency { get; set; } = 5;
    public WorkflowTimeoutSettings TimeoutSettings { get; set; } = new();
}

/// <summary>
/// Timeout settings for workflow execution.
/// </summary>
public class WorkflowTimeoutSettings
{
    public TimeSpan StepTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan OverallTimeout { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan CompensationTimeout { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Validation result for saga workflow configuration.
/// </summary>
public class SagaWorkflowValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// Interfaces for fluent builder pattern

public interface ISagaWorkflowBuilder<TSagaData>
    where TSagaData : class
{
    ISagaStepBuilder<TSagaData> AddStep(string stepName);
    ISagaStepBuilder<TSagaData> AddStep(string stepName, ISagaStep<TSagaData> implementation);
    ISagaStepBuilder<TSagaData> AddStep(string stepName, 
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null);
    IParallelBranchBuilder<TSagaData> CreateParallelBranch(string branchName);
    IConditionalBranchBuilder<TSagaData> CreateConditionalBranch(Func<TSagaData, Task<bool>> condition);
    SagaWorkflowValidationResult Validate();
    SagaWorkflowConfiguration<TSagaData> GetConfiguration();
}

public interface ISagaStepBuilder<TSagaData>
    where TSagaData : class
{
    ISagaStepBuilder<TSagaData> DependsOn(params string[] stepNames);
    ISagaStepBuilder<TSagaData> WithImplementation(ISagaStep<TSagaData> implementation);
    ISagaStepBuilder<TSagaData> WithExecution(
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null);
    ISagaStepBuilder<TSagaData> ContinueOnFailure(bool continueOnFailure = true);
    ISagaStepBuilder<TSagaData> WithCondition(Func<SagaConditionContext<TSagaData>, Task<bool>> condition);
    ISagaStepBuilder<TSagaData> WithRetryPolicy(int maxRetries, TimeSpan retryDelay);
    ISagaStepBuilder<TSagaData> WithMetadata(string key, object value);
    ISagaWorkflowBuilder<TSagaData> And();
}

public interface IParallelBranchBuilder<TSagaData>
    where TSagaData : class
{
    IParallelBranchBuilder<TSagaData> AddParallelStep(string stepName, ISagaStep<TSagaData> implementation);
    IParallelBranchBuilder<TSagaData> AddParallelStep(string stepName,
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null);
    ISagaWorkflowBuilder<TSagaData> ThenJoin(string joinStepName, ISagaStep<TSagaData> joinImplementation);
}

public interface IConditionalBranchBuilder<TSagaData>
    where TSagaData : class
{
    ISagaWorkflowBuilder<TSagaData> Then(string stepName, ISagaStep<TSagaData> implementation);
    ISagaWorkflowBuilder<TSagaData> Then(string stepName,
        Func<TSagaData, SagaContext, Task<SagaStepResult>> executeFunc,
        Func<TSagaData, SagaStepResult?, SagaContext, Task<CompensationResult>>? compensateFunc = null);
}