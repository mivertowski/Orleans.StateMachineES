using System;
using System.Collections.Generic;
using Orleans.StateMachineES.EventSourcing;
using Orleans;

namespace Orleans.StateMachineES.Sagas;

/// <summary>
/// Interface for saga grain state that contains saga-specific data.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
public interface ISagaGrainState<TSagaData>
    where TSagaData : class
{
    /// <summary>
    /// The data being processed by the saga.
    /// </summary>
    TSagaData? SagaData { get; set; }
    
    /// <summary>
    /// The saga execution context.
    /// </summary>
    SagaContext? SagaContext { get; set; }
    
    /// <summary>
    /// The current status of the saga.
    /// </summary>
    SagaStatus Status { get; set; }
    
    /// <summary>
    /// When the saga was started.
    /// </summary>
    DateTime StartTime { get; set; }
    
    /// <summary>
    /// When the saga completed (if completed).
    /// </summary>
    DateTime? CompletionTime { get; set; }
    
    /// <summary>
    /// Error message if the saga failed.
    /// </summary>
    string? ErrorMessage { get; set; }
    
    /// <summary>
    /// History of compensation executions.
    /// </summary>
    List<CompensationExecution> CompensationHistory { get; set; }
}

/// <summary>
/// Base state class for saga coordinator grains.
/// </summary>
/// <typeparam name="TSagaData">The type of data processed by the saga.</typeparam>
[GenerateSerializer]
public class SagaState<TSagaData> : EventSourcedStateMachineState<SagaStatus>, ISagaGrainState<TSagaData>
    where TSagaData : class
{
    /// <summary>
    /// The data being processed by the saga.
    /// </summary>
    [Id(0)]
    public TSagaData? SagaData { get; set; }
    
    /// <summary>
    /// The saga execution context.
    /// </summary>
    [Id(1)]
    public SagaContext? SagaContext { get; set; }
    
    /// <summary>
    /// The current status of the saga.
    /// </summary>
    [Id(2)]
    public SagaStatus Status { get; set; } = SagaStatus.NotStarted;
    
    /// <summary>
    /// When the saga was started.
    /// </summary>
    [Id(3)]
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// When the saga completed (if completed).
    /// </summary>
    [Id(4)]
    public DateTime? CompletionTime { get; set; }
    
    /// <summary>
    /// Error message if the saga failed.
    /// </summary>
    [Id(5)]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// The index of the currently executing step.
    /// </summary>
    [Id(6)]
    public int CurrentStepIndex { get; set; } = -1;
    
    /// <summary>
    /// History of compensation executions.
    /// </summary>
    [Id(7)]
    public List<CompensationExecution> CompensationHistory { get; set; } = new();
    
    /// <summary>
    /// The results from each step execution for use in compensation.
    /// </summary>
    [Id(8)]
    public Dictionary<string, object> StepResults { get; set; } = new();
    
    /// <summary>
    /// Total number of retry attempts across all steps.
    /// </summary>
    [Id(9)]
    public int TotalRetryAttempts { get; set; }
    
    /// <summary>
    /// Custom properties for the saga state.
    /// </summary>
    [Id(10)]
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Triggers for saga state machine transitions.
/// </summary>
public enum SagaTrigger
{
    /// <summary>
    /// Start the saga execution.
    /// </summary>
    Start = 0,
    
    /// <summary>
    /// A step completed successfully.
    /// </summary>
    StepCompleted = 1,
    
    /// <summary>
    /// A step failed with a business error.
    /// </summary>
    StepFailedBusiness = 2,
    
    /// <summary>
    /// A step failed with a technical error.
    /// </summary>
    StepFailedTechnical = 3,
    
    /// <summary>
    /// Start compensation process.
    /// </summary>
    StartCompensation = 4,
    
    /// <summary>
    /// Compensation completed successfully.
    /// </summary>
    CompensationCompleted = 5,
    
    /// <summary>
    /// Compensation failed.
    /// </summary>
    CompensationFailed = 6,
    
    /// <summary>
    /// Saga completed successfully.
    /// </summary>
    Complete = 7,
    
    /// <summary>
    /// Mark saga as failed.
    /// </summary>
    Fail = 8,
    
    /// <summary>
    /// Timeout occurred during execution.
    /// </summary>
    Timeout = 9,
    
    /// <summary>
    /// Cancel the saga execution.
    /// </summary>
    Cancel = 10
}