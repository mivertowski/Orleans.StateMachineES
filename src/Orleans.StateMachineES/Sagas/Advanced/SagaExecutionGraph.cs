using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Orleans.StateMachineES.Sagas.Advanced;

/// <summary>
/// Represents the execution graph for a parallel saga with dependency management and topological ordering.
/// Manages the workflow structure and provides methods for dependency resolution and execution planning.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class SagaExecutionGraph<TSagaData>
    where TSagaData : class
{
    private readonly Dictionary<string, SagaWorkflowStep<TSagaData>> _steps = new();
    private readonly Dictionary<string, List<string>> _dependencies = new();
    private readonly Dictionary<string, List<string>> _dependents = new();

    /// <summary>
    /// Builds the execution graph from the workflow configuration.
    /// </summary>
    public void BuildFromConfiguration(SagaWorkflowConfiguration<TSagaData> configuration)
    {
        _steps.Clear();
        _dependencies.Clear();
        _dependents.Clear();

        // Add all steps
        foreach (var step in configuration.Steps)
        {
            _steps[step.Name] = step;
            _dependencies[step.Name] = step.Dependencies.ToList();
            _dependents[step.Name] = new List<string>();
        }

        // Build dependent relationships
        foreach (var kvp in _dependencies)
        {
            var stepName = kvp.Key;
            var dependencies = kvp.Value;

            foreach (var dependency in dependencies)
            {
                if (_dependents.ContainsKey(dependency))
                {
                    _dependents[dependency].Add(stepName);
                }
            }
        }
    }

    /// <summary>
    /// Gets all steps that have no dependencies (entry points).
    /// </summary>
    public IEnumerable<SagaWorkflowStep<TSagaData>> GetEntrySteps()
    {
        return _steps.Values.Where(step => !step.Dependencies.Any());
    }

    /// <summary>
    /// Gets all steps that depend on the specified step.
    /// </summary>
    public IEnumerable<SagaWorkflowStep<TSagaData>> GetDependentSteps(string stepName)
    {
        if (!_dependents.ContainsKey(stepName))
            return Enumerable.Empty<SagaWorkflowStep<TSagaData>>();

        return _dependents[stepName].Select(name => _steps[name]);
    }

    /// <summary>
    /// Checks if a step exists in the execution graph.
    /// </summary>
    public bool HasStep(string stepName)
    {
        return _steps.ContainsKey(stepName);
    }

    /// <summary>
    /// Gets a step by name.
    /// </summary>
    public SagaWorkflowStep<TSagaData> GetStep(string stepName)
    {
        if (!_steps.TryGetValue(stepName, out var step))
        {
            throw new ArgumentException($"Step '{stepName}' not found in execution graph", nameof(stepName));
        }
        return step;
    }

    /// <summary>
    /// Gets all steps in topological order for sequential execution.
    /// </summary>
    public List<SagaWorkflowStep<TSagaData>> GetTopologicalOrder()
    {
        var result = new List<SagaWorkflowStep<TSagaData>>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var step in _steps.Values)
        {
            if (!visited.Contains(step.Name))
            {
                TopologicalSortUtil(step.Name, visited, visiting, result);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets execution levels where each level can be executed in parallel.
    /// </summary>
    public List<List<SagaWorkflowStep<TSagaData>>> GetExecutionLevels()
    {
        var levels = new List<List<SagaWorkflowStep<TSagaData>>>();
        var processed = new HashSet<string>();
        var inDegree = new Dictionary<string, int>();

        // Calculate in-degrees
        foreach (var step in _steps.Values)
        {
            inDegree[step.Name] = step.Dependencies.Count;
        }

        while (processed.Count < _steps.Count)
        {
            var currentLevel = new List<SagaWorkflowStep<TSagaData>>();

            // Find all steps with in-degree 0 that haven't been processed
            foreach (var step in _steps.Values)
            {
                if (!processed.Contains(step.Name) && inDegree[step.Name] == 0)
                {
                    currentLevel.Add(step);
                }
            }

            if (!currentLevel.Any())
            {
                throw new InvalidOperationException("Circular dependency detected in saga workflow");
            }

            levels.Add(currentLevel);

            // Mark as processed and update in-degrees
            foreach (var step in currentLevel)
            {
                processed.Add(step.Name);

                // Reduce in-degree for dependent steps
                if (_dependents.ContainsKey(step.Name))
                {
                    foreach (var dependent in _dependents[step.Name])
                    {
                        inDegree[dependent]--;
                    }
                }
            }
        }

        return levels;
    }

    /// <summary>
    /// Validates the execution graph for cycles and orphaned dependencies.
    /// </summary>
    public SagaGraphValidationResult Validate()
    {
        var result = new SagaGraphValidationResult { IsValid = true };

        // Check for circular dependencies
        try
        {
            GetTopologicalOrder();
        }
        catch (InvalidOperationException ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Circular dependency detected: {ex.Message}");
        }

        // Check for orphaned dependencies
        foreach (var step in _steps.Values)
        {
            foreach (var dependency in step.Dependencies)
            {
                if (!_steps.ContainsKey(dependency))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Step '{step.Name}' depends on non-existent step '{dependency}'");
                }
            }
        }

        // Check for unreachable steps
        var reachableSteps = new HashSet<string>();
        var entrySteps = GetEntrySteps().Select(s => s.Name).ToList();
        
        foreach (var entryStep in entrySteps)
        {
            TraverseReachableSteps(entryStep, reachableSteps);
        }

        var unreachableSteps = _steps.Keys.Except(reachableSteps).ToList();
        foreach (var unreachableStep in unreachableSteps)
        {
            result.Warnings.Add($"Step '{unreachableStep}' is not reachable from any entry point");
        }

        return result;
    }

    /// <summary>
    /// Gets execution statistics for the workflow graph.
    /// </summary>
    public SagaGraphStatistics GetStatistics()
    {
        var levels = GetExecutionLevels();
        
        return new SagaGraphStatistics
        {
            TotalSteps = _steps.Count,
            EntryPoints = GetEntrySteps().Count(),
            MaxParallelism = levels.Max(level => level.Count),
            ExecutionLevels = levels.Count,
            AverageStepsPerLevel = levels.Average(level => level.Count),
            CriticalPathLength = CalculateCriticalPathLength(),
            ComplexityScore = CalculateComplexityScore()
        };
    }

    private void TopologicalSortUtil(string stepName, HashSet<string> visited, HashSet<string> visiting, List<SagaWorkflowStep<TSagaData>> result)
    {
        if (visiting.Contains(stepName))
        {
            throw new InvalidOperationException($"Circular dependency detected involving step '{stepName}'");
        }

        if (visited.Contains(stepName))
            return;

        visiting.Add(stepName);

        var step = _steps[stepName];
        foreach (var dependency in step.Dependencies)
        {
            TopologicalSortUtil(dependency, visited, visiting, result);
        }

        visiting.Remove(stepName);
        visited.Add(stepName);
        result.Add(step);
    }

    private void TraverseReachableSteps(string stepName, HashSet<string> reachableSteps)
    {
        if (reachableSteps.Contains(stepName))
            return;

        reachableSteps.Add(stepName);

        if (_dependents.ContainsKey(stepName))
        {
            foreach (var dependent in _dependents[stepName])
            {
                TraverseReachableSteps(dependent, reachableSteps);
            }
        }
    }

    private int CalculateCriticalPathLength()
    {
        var memo = new Dictionary<string, int>();
        var maxLength = 0;

        foreach (var step in _steps.Values)
        {
            maxLength = Math.Max(maxLength, CalculateCriticalPathFromStep(step.Name, memo));
        }

        return maxLength;
    }

    private int CalculateCriticalPathFromStep(string stepName, Dictionary<string, int> memo)
    {
        if (memo.ContainsKey(stepName))
            return memo[stepName];

        var step = _steps[stepName];
        var maxDependencyLength = 0;

        foreach (var dependency in step.Dependencies)
        {
            maxDependencyLength = Math.Max(maxDependencyLength, CalculateCriticalPathFromStep(dependency, memo));
        }

        var length = maxDependencyLength + 1;
        memo[stepName] = length;
        return length;
    }

    private double CalculateComplexityScore()
    {
        // Complexity based on number of steps, dependencies, and branching factor
        var totalDependencies = _dependencies.Values.Sum(deps => deps.Count);
        var maxBranchingFactor = _dependents.Values.Max(deps => deps.Count);
        
        return (_steps.Count * 0.4) + (totalDependencies * 0.4) + (maxBranchingFactor * 0.2);
    }
}

/// <summary>
/// Represents a workflow step in the saga execution graph.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public class SagaWorkflowStep<TSagaData>
    where TSagaData : class
{
    public string Name { get; set; } = "";
    public List<string> Dependencies { get; set; } = new();
    public bool ContinueOnFailure { get; set; }
    public Func<SagaConditionContext<TSagaData>, Task<bool>>? Condition { get; set; }
    public ISagaStep<TSagaData> Implementation { get; set; } = default!;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public Dictionary<string, object> Metadata { get; set; } = new();

    public async Task<SagaStepResult> ExecuteAsync(TSagaData sagaData, SagaContext context)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= MaxRetries)
        {
            try
            {
                return await Implementation.ExecuteAsync(sagaData, context);
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                if (attempt <= MaxRetries)
                {
                    await Task.Delay(RetryDelay * attempt); // Exponential backoff
                }
            }
        }

        return SagaStepResult.TechnicalFailure(
            $"Step failed after {MaxRetries} retries: {lastException?.Message}",
            lastException);
    }

    public async Task<CompensationResult> CompensateAsync(TSagaData sagaData, SagaStepResult? originalResult, SagaContext context)
    {
        try
        {
            return await Implementation.CompensateAsync(sagaData, originalResult, context);
        }
        catch (Exception ex)
        {
            return CompensationResult.Failure($"Compensation failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Validation result for saga execution graph.
/// </summary>
public class SagaGraphValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Statistical information about the saga execution graph.
/// </summary>
public class SagaGraphStatistics
{
    public int TotalSteps { get; set; }
    public int EntryPoints { get; set; }
    public int MaxParallelism { get; set; }
    public int ExecutionLevels { get; set; }
    public double AverageStepsPerLevel { get; set; }
    public int CriticalPathLength { get; set; }
    public double ComplexityScore { get; set; }
}