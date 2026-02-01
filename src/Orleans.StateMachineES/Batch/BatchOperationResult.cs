namespace Orleans.StateMachineES.Batch;

/// <summary>
/// Result of a batch operation containing all item results.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Batch.BatchOperationResult`1")]
public class BatchOperationResult<TState>
{
    /// <summary>
    /// Total number of operations in the batch.
    /// </summary>
    [Id(0)]
    public int TotalOperations { get; set; }

    /// <summary>
    /// Number of successful operations.
    /// </summary>
    [Id(1)]
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed operations.
    /// </summary>
    [Id(2)]
    public int FailureCount { get; set; }

    /// <summary>
    /// Number of skipped operations (due to StopOnFirstFailure or cancellation).
    /// </summary>
    [Id(3)]
    public int SkippedCount { get; set; }

    /// <summary>
    /// Total duration of the batch operation.
    /// </summary>
    [Id(4)]
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// When the batch started.
    /// </summary>
    [Id(5)]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the batch completed.
    /// </summary>
    [Id(6)]
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Individual results for each item.
    /// </summary>
    [Id(7)]
    public IReadOnlyList<BatchItemResult<TState>> Results { get; set; } = Array.Empty<BatchItemResult<TState>>();

    /// <summary>
    /// Whether the entire batch was successful.
    /// </summary>
    public bool IsFullySuccessful => FailureCount == 0 && SkippedCount == 0;

    /// <summary>
    /// Whether the batch was partially successful.
    /// </summary>
    public bool IsPartiallySuccessful => SuccessCount > 0 && (FailureCount > 0 || SkippedCount > 0);

    /// <summary>
    /// Whether the entire batch failed.
    /// </summary>
    public bool IsFullyFailed => SuccessCount == 0 && TotalOperations > 0;

    /// <summary>
    /// Success rate as a percentage.
    /// </summary>
    public double SuccessRate => TotalOperations > 0
        ? Math.Round((double)SuccessCount / TotalOperations * 100, 2)
        : 0;

    /// <summary>
    /// Average operation duration.
    /// </summary>
    public TimeSpan AverageOperationDuration => TotalOperations > 0
        ? TimeSpan.FromTicks(Duration.Ticks / TotalOperations)
        : TimeSpan.Zero;

    /// <summary>
    /// Gets failed results only.
    /// </summary>
    public IEnumerable<BatchItemResult<TState>> GetFailedResults()
        => Results.Where(r => !r.IsSuccess);

    /// <summary>
    /// Gets successful results only.
    /// </summary>
    public IEnumerable<BatchItemResult<TState>> GetSuccessfulResults()
        => Results.Where(r => r.IsSuccess);

    /// <summary>
    /// Groups results by error type.
    /// </summary>
    public IEnumerable<IGrouping<string?, BatchItemResult<TState>>> GetResultsByErrorType()
        => Results.Where(r => !r.IsSuccess).GroupBy(r => r.ExceptionType);
}
