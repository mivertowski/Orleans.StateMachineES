namespace Orleans.StateMachineES.Batch;

/// <summary>
/// Represents a single operation request in a batch.
/// </summary>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Batch.BatchOperationRequest`1")]
public class BatchOperationRequest<TTrigger>
{
    /// <summary>
    /// The grain ID to target.
    /// </summary>
    [Id(0)]
    public string GrainId { get; set; } = string.Empty;

    /// <summary>
    /// The trigger to fire.
    /// </summary>
    [Id(1)]
    public TTrigger Trigger { get; set; } = default!;

    /// <summary>
    /// Optional arguments for parameterized triggers.
    /// </summary>
    [Id(2)]
    public object[]? Arguments { get; set; }

    /// <summary>
    /// Optional correlation ID for tracking related operations.
    /// </summary>
    [Id(3)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Optional metadata for the operation.
    /// </summary>
    [Id(4)]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Priority of this operation (higher = processed first).
    /// Default: 0
    /// </summary>
    [Id(5)]
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Creates a new batch operation request.
    /// </summary>
    public static BatchOperationRequest<TTrigger> Create(
        string grainId,
        TTrigger trigger,
        object[]? arguments = null,
        string? correlationId = null)
    {
        return new BatchOperationRequest<TTrigger>
        {
            GrainId = grainId,
            Trigger = trigger,
            Arguments = arguments,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
/// Non-generic batch operation request for runtime scenarios.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Batch.BatchOperationRequest")]
public class BatchOperationRequest
{
    /// <summary>
    /// The grain ID to target.
    /// </summary>
    [Id(0)]
    public string GrainId { get; set; } = string.Empty;

    /// <summary>
    /// The trigger to fire (boxed).
    /// </summary>
    [Id(1)]
    public object Trigger { get; set; } = default!;

    /// <summary>
    /// Optional arguments for parameterized triggers.
    /// </summary>
    [Id(2)]
    public object[]? Arguments { get; set; }

    /// <summary>
    /// Optional correlation ID for tracking related operations.
    /// </summary>
    [Id(3)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Optional metadata for the operation.
    /// </summary>
    [Id(4)]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Priority of this operation (higher = processed first).
    /// Default: 0
    /// </summary>
    [Id(5)]
    public int Priority { get; set; } = 0;
}
