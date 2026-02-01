namespace Orleans.StateMachineES.Batch;

/// <summary>
/// Configuration options for batch operations.
/// </summary>
public class BatchOperationOptions
{
    /// <summary>
    /// Maximum number of concurrent operations.
    /// Default: 10
    /// </summary>
    public int MaxParallelism { get; set; } = 10;

    /// <summary>
    /// Whether to stop the batch on the first failure.
    /// Default: false
    /// </summary>
    public bool StopOnFirstFailure { get; set; } = false;

    /// <summary>
    /// Overall timeout for the entire batch operation.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Timeout for individual operations within the batch.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to continue processing remaining items on error.
    /// Default: true
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Whether to order operations by priority before execution.
    /// Default: false
    /// </summary>
    public bool OrderByPriority { get; set; } = false;

    /// <summary>
    /// Whether to retry failed operations.
    /// Default: false
    /// </summary>
    public bool EnableRetry { get; set; } = false;

    /// <summary>
    /// Maximum retry attempts for failed operations.
    /// Default: 3
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts.
    /// Default: 1 second
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to use exponential backoff for retries.
    /// Default: true
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Callback invoked when a batch item completes (success or failure).
    /// </summary>
    public Action<BatchItemCompletedEventArgs>? OnItemCompleted { get; set; }

    /// <summary>
    /// Callback invoked when the batch starts.
    /// </summary>
    public Action<int>? OnBatchStarted { get; set; }

    /// <summary>
    /// Callback invoked when the batch completes.
    /// </summary>
    public Action<BatchCompletedEventArgs>? OnBatchCompleted { get; set; }

    /// <summary>
    /// Correlation ID for the entire batch operation.
    /// </summary>
    public string? BatchCorrelationId { get; set; }
}

/// <summary>
/// Event args for batch item completion.
/// </summary>
public class BatchItemCompletedEventArgs
{
    /// <summary>
    /// The grain ID.
    /// </summary>
    public string GrainId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Index in the batch.
    /// </summary>
    public int BatchIndex { get; set; }

    /// <summary>
    /// Current progress (completed count).
    /// </summary>
    public int CompletedCount { get; set; }

    /// <summary>
    /// Total items in batch.
    /// </summary>
    public int TotalCount { get; set; }
}

/// <summary>
/// Event args for batch completion.
/// </summary>
public class BatchCompletedEventArgs
{
    /// <summary>
    /// Number of successful operations.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed operations.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Total duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether the batch was cancelled.
    /// </summary>
    public bool WasCancelled { get; set; }
}
