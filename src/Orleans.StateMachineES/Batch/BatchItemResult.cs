namespace Orleans.StateMachineES.Batch;

/// <summary>
/// Result of a single batch item operation.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Batch.BatchItemResult`1")]
public class BatchItemResult<TState>
{
    /// <summary>
    /// The grain ID that was targeted.
    /// </summary>
    [Id(0)]
    public string GrainId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    [Id(1)]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The state before the transition (if successful).
    /// </summary>
    [Id(2)]
    public TState? FromState { get; set; }

    /// <summary>
    /// The state after the transition (if successful).
    /// </summary>
    [Id(3)]
    public TState? ToState { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    [Id(4)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception type if the operation failed.
    /// </summary>
    [Id(5)]
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Duration of the operation.
    /// </summary>
    [Id(6)]
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Correlation ID if provided in the request.
    /// </summary>
    [Id(7)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Index of this item in the original batch.
    /// </summary>
    [Id(8)]
    public int BatchIndex { get; set; }

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static BatchItemResult<TState> Success(
        string grainId,
        TState fromState,
        TState toState,
        TimeSpan duration,
        int batchIndex,
        string? correlationId = null)
    {
        return new BatchItemResult<TState>
        {
            GrainId = grainId,
            IsSuccess = true,
            FromState = fromState,
            ToState = toState,
            Duration = duration,
            BatchIndex = batchIndex,
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static BatchItemResult<TState> Failure(
        string grainId,
        string errorMessage,
        string? exceptionType,
        TimeSpan duration,
        int batchIndex,
        string? correlationId = null)
    {
        return new BatchItemResult<TState>
        {
            GrainId = grainId,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ExceptionType = exceptionType,
            Duration = duration,
            BatchIndex = batchIndex,
            CorrelationId = correlationId
        };
    }
}
