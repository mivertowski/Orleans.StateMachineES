using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace ivlt.Orleans.StateMachineES.Sagas;

/// <summary>
/// Interface for saga coordinator grains that orchestrate distributed transactions.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public interface ISagaCoordinatorGrain<TSagaData> : IGrainWithStringKey
    where TSagaData : class
{
    /// <summary>
    /// Executes the saga with the provided data.
    /// </summary>
    /// <param name="sagaData">The data to process through the saga.</param>
    /// <param name="correlationId">Optional correlation ID for tracking.</param>
    /// <returns>The result of the saga execution.</returns>
    Task<SagaExecutionResult> ExecuteAsync(TSagaData sagaData, string? correlationId = null);
    
    /// <summary>
    /// Compensates the saga by rolling back completed steps.
    /// </summary>
    /// <param name="reason">The reason for compensation.</param>
    /// <returns>The result of the compensation process.</returns>
    Task<CompensationResult> CompensateAsync(string reason);
    
    /// <summary>
    /// Gets the current status of the saga.
    /// </summary>
    /// <returns>The saga status information.</returns>
    Task<SagaStatusInfo> GetStatusAsync();
    
    /// <summary>
    /// Gets the execution history of the saga.
    /// </summary>
    /// <returns>The complete execution history.</returns>
    Task<SagaExecutionHistory> GetHistoryAsync();
}

/// <summary>
/// Overall result of saga execution.
/// </summary>
[GenerateSerializer]
public class SagaExecutionResult
{
    /// <summary>
    /// Whether the saga completed successfully.
    /// </summary>
    [Id(0)]
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Whether compensation was performed.
    /// </summary>
    [Id(1)]
    public bool IsCompensated { get; set; }
    
    /// <summary>
    /// Error message if the saga failed.
    /// </summary>
    [Id(2)]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// The execution history of all steps.
    /// </summary>
    [Id(3)]
    public IReadOnlyList<SagaStepExecution>? ExecutionHistory { get; set; }
    
    /// <summary>
    /// The compensation history if compensation was performed.
    /// </summary>
    [Id(4)]
    public IList<CompensationExecution>? CompensationHistory { get; set; }
    
    /// <summary>
    /// Total duration of the saga execution.
    /// </summary>
    [Id(5)]
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// When the saga execution completed.
    /// </summary>
    [Id(6)]
    public DateTime CompletionTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Current status information of a saga.
/// </summary>
[GenerateSerializer]
public class SagaStatusInfo
{
    /// <summary>
    /// The saga identifier.
    /// </summary>
    [Id(0)]
    public string SagaId { get; set; } = string.Empty;
    
    /// <summary>
    /// The current status of the saga.
    /// </summary>
    [Id(1)]
    public SagaStatus Status { get; set; }
    
    /// <summary>
    /// When the saga started.
    /// </summary>
    [Id(2)]
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When the saga completed (if completed).
    /// </summary>
    [Id(3)]
    public DateTime? CompletionTime { get; set; }
    
    /// <summary>
    /// Error message if the saga failed.
    /// </summary>
    [Id(4)]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// The index of the currently executing step.
    /// </summary>
    [Id(5)]
    public int CurrentStepIndex { get; set; }
    
    /// <summary>
    /// The name of the currently executing step.
    /// </summary>
    [Id(6)]
    public string? CurrentStepName { get; set; }
    
    /// <summary>
    /// The total number of steps in the saga.
    /// </summary>
    [Id(7)]
    public int TotalSteps { get; set; }
    
    /// <summary>
    /// The execution history of completed steps.
    /// </summary>
    [Id(8)]
    public IReadOnlyList<SagaStepExecution>? ExecutionHistory { get; set; }
    
    /// <summary>
    /// The compensation history if compensation was performed.
    /// </summary>
    [Id(9)]
    public IList<CompensationExecution>? CompensationHistory { get; set; }
}

/// <summary>
/// Complete execution history of a saga.
/// </summary>
[GenerateSerializer]
public class SagaExecutionHistory
{
    /// <summary>
    /// The saga identifier.
    /// </summary>
    [Id(0)]
    public string SagaId { get; set; } = string.Empty;
    
    /// <summary>
    /// The correlation ID for the saga.
    /// </summary>
    [Id(1)]
    public string CorrelationId { get; set; } = string.Empty;
    
    /// <summary>
    /// When the saga started.
    /// </summary>
    [Id(2)]
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When the saga completed.
    /// </summary>
    [Id(3)]
    public DateTime? CompletionTime { get; set; }
    
    /// <summary>
    /// The final status of the saga.
    /// </summary>
    [Id(4)]
    public SagaStatus Status { get; set; }
    
    /// <summary>
    /// The execution history of all steps.
    /// </summary>
    [Id(5)]
    public IReadOnlyList<SagaStepExecution>? StepExecutions { get; set; }
    
    /// <summary>
    /// The compensation history if compensation was performed.
    /// </summary>
    [Id(6)]
    public IList<CompensationExecution>? CompensationExecutions { get; set; }
    
    /// <summary>
    /// Total duration of the saga.
    /// </summary>
    [Id(7)]
    public TimeSpan TotalDuration { get; set; }
}

/// <summary>
/// Record of a saga step execution.
/// </summary>
[GenerateSerializer]
public class SagaStepExecution
{
    /// <summary>
    /// The index of the step in the saga.
    /// </summary>
    [Id(0)]
    public int StepIndex { get; set; }
    
    /// <summary>
    /// The name of the step.
    /// </summary>
    [Id(1)]
    public string StepName { get; set; } = string.Empty;
    
    /// <summary>
    /// When the step was executed.
    /// </summary>
    [Id(2)]
    public DateTime ExecutionTime { get; set; }
    
    /// <summary>
    /// How long the step took to execute.
    /// </summary>
    [Id(3)]
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Whether the step executed successfully.
    /// </summary>
    [Id(4)]
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Error message if the step failed.
    /// </summary>
    [Id(5)]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// The result of the step execution.
    /// </summary>
    [Id(6)]
    public object? Result { get; set; }
    
    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    [Id(7)]
    public int RetryAttempts { get; set; }
}

/// <summary>
/// Record of a compensation execution.
/// </summary>
[GenerateSerializer]
public class CompensationExecution
{
    /// <summary>
    /// The index of the step that was compensated.
    /// </summary>
    [Id(0)]
    public int StepIndex { get; set; }
    
    /// <summary>
    /// The name of the step that was compensated.
    /// </summary>
    [Id(1)]
    public string StepName { get; set; } = string.Empty;
    
    /// <summary>
    /// When the compensation was executed.
    /// </summary>
    [Id(2)]
    public DateTime ExecutionTime { get; set; }
    
    /// <summary>
    /// How long the compensation took.
    /// </summary>
    [Id(3)]
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Whether the compensation was successful.
    /// </summary>
    [Id(4)]
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Error message if compensation failed.
    /// </summary>
    [Id(5)]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of a saga execution.
/// </summary>
public enum SagaStatus
{
    /// <summary>
    /// The saga has not been started.
    /// </summary>
    NotStarted = 0,
    
    /// <summary>
    /// The saga is currently running.
    /// </summary>
    Running = 1,
    
    /// <summary>
    /// The saga completed successfully.
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// The saga failed and is being compensated.
    /// </summary>
    Compensating = 3,
    
    /// <summary>
    /// The saga was compensated successfully.
    /// </summary>
    Compensated = 4,
    
    /// <summary>
    /// The saga failed and compensation also failed.
    /// </summary>
    CompensationFailed = 5,
    
    /// <summary>
    /// The saga failed without attempting compensation.
    /// </summary>
    Failed = 6
}