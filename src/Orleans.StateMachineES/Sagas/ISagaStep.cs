using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace Orleans.StateMachineES.Sagas;

/// <summary>
/// Interface for saga steps that can be executed and compensated.
/// </summary>
/// <typeparam name="TSagaData">The type of data passed between saga steps.</typeparam>
public interface ISagaStep<TSagaData>
    where TSagaData : class
{
    /// <summary>
    /// The name of this saga step for identification.
    /// </summary>
    string StepName { get; }
    
    /// <summary>
    /// Executes the saga step with the provided data and context.
    /// </summary>
    /// <param name="sagaData">The saga data.</param>
    /// <param name="context">The saga execution context.</param>
    /// <returns>The result of the step execution.</returns>
    Task<SagaStepResult> ExecuteAsync(TSagaData sagaData, SagaContext context);
    
    /// <summary>
    /// Compensates for this saga step when the saga needs to be rolled back.
    /// </summary>
    /// <param name="sagaData">The saga data.</param>
    /// <param name="stepResult">The result from the original step execution, if available.</param>
    /// <param name="context">The saga execution context.</param>
    /// <returns>The result of the compensation.</returns>
    Task<CompensationResult> CompensateAsync(TSagaData sagaData, SagaStepResult? stepResult, SagaContext context);
    
    /// <summary>
    /// The timeout for this step execution.
    /// </summary>
    TimeSpan Timeout { get; }
    
    /// <summary>
    /// Whether this step can be retried on technical failures.
    /// </summary>
    bool CanRetry { get; }
    
    /// <summary>
    /// The maximum number of retry attempts.
    /// </summary>
    int MaxRetryAttempts { get; }
}

/// <summary>
/// Result of executing a saga step.
/// </summary>
[GenerateSerializer]
public class SagaStepResult
{
    [Id(0)] public bool IsSuccess { get; set; }
    [Id(1)] public bool IsBusinessFailure { get; set; }
    [Id(2)] public bool IsTechnicalFailure { get; set; }
    [Id(3)] public object? Result { get; set; }
    [Id(4)] public string? ErrorMessage { get; set; }
    [Id(5)] public Exception? Exception { get; set; }
    [Id(6)] public DateTime ExecutionTime { get; set; }
    [Id(7)] public TimeSpan Duration { get; set; }

    private SagaStepResult(bool isSuccess, bool isBusinessFailure, bool isTechnicalFailure, 
        object? result, string? errorMessage, Exception? exception, TimeSpan duration)
    {
        IsSuccess = isSuccess;
        IsBusinessFailure = isBusinessFailure;
        IsTechnicalFailure = isTechnicalFailure;
        Result = result;
        ErrorMessage = errorMessage;
        Exception = exception;
        ExecutionTime = DateTime.UtcNow;
        Duration = duration;
    }

    // Required for Orleans serialization
    public SagaStepResult() { }

    /// <summary>
    /// Creates a successful step result.
    /// </summary>
    public static SagaStepResult Success(object? result = null, TimeSpan duration = default)
        => new(true, false, false, result, null, null, duration);

    /// <summary>
    /// Creates a business failure step result (should trigger compensation).
    /// </summary>
    public static SagaStepResult BusinessFailure(string errorMessage, TimeSpan duration = default)
        => new(false, true, false, null, errorMessage, null, duration);

    /// <summary>
    /// Creates a technical failure step result (should trigger retry, then compensation).
    /// </summary>
    public static SagaStepResult TechnicalFailure(string errorMessage, Exception? exception = null, TimeSpan duration = default)
        => new(false, false, true, null, errorMessage, exception, duration);
}

/// <summary>
/// Result of executing a compensation action.
/// </summary>
[GenerateSerializer]
public class CompensationResult
{
    [Id(0)] public bool IsSuccess { get; set; }
    [Id(1)] public string? ErrorMessage { get; set; }
    [Id(2)] public Exception? Exception { get; set; }
    [Id(3)] public DateTime ExecutionTime { get; set; }
    [Id(4)] public TimeSpan Duration { get; set; }

    private CompensationResult(bool isSuccess, string? errorMessage, Exception? exception, TimeSpan duration)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
        ExecutionTime = DateTime.UtcNow;
        Duration = duration;
    }

    // Required for Orleans serialization
    public CompensationResult() { }

    /// <summary>
    /// Creates a successful compensation result.
    /// </summary>
    public static CompensationResult Success(TimeSpan duration = default)
        => new(true, null, null, duration);

    /// <summary>
    /// Creates a failed compensation result.
    /// </summary>
    public static CompensationResult Failure(string errorMessage, Exception? exception = null, TimeSpan duration = default)
        => new(false, errorMessage, exception, duration);
}

/// <summary>
/// Context information passed to saga steps.
/// </summary>
[GenerateSerializer]
public class SagaContext
{
    /// <summary>
    /// Unique identifier for the saga instance.
    /// </summary>
    [Id(0)] public string SagaId { get; init; } = string.Empty;
    
    /// <summary>
    /// Correlation ID for tracking distributed operations.
    /// </summary>
    [Id(1)] public string CorrelationId { get; init; } = string.Empty;
    
    /// <summary>
    /// Business transaction identifier.
    /// </summary>
    [Id(2)] public string BusinessTransactionId { get; init; } = string.Empty;
    
    /// <summary>
    /// The user or system that initiated the saga.
    /// </summary>
    [Id(3)] public string InitiatedBy { get; init; } = string.Empty;
    
    /// <summary>
    /// When the saga was started.
    /// </summary>
    [Id(4)] public DateTime StartTime { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Additional metadata for the saga context.
    /// </summary>
    [Id(5)] public Dictionary<string, object> Metadata { get; init; } = new();
    
    /// <summary>
    /// Current step index in the saga execution.
    /// </summary>
    [Id(6)] public int CurrentStepIndex { get; set; }
    
    /// <summary>
    /// The name of the current step being executed.
    /// </summary>
    [Id(7)] public string? CurrentStepName { get; set; }
    
    /// <summary>
    /// Whether the saga is currently in compensation mode.
    /// </summary>
    [Id(8)] public bool IsCompensating { get; set; }
}