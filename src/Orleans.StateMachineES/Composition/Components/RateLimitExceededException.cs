namespace Orleans.StateMachineES.Composition.Components;

/// <summary>
/// Exception thrown when rate limit is exceeded and blocking is configured.
/// </summary>
public class RateLimitExceededException : InvalidOperationException
{
    /// <summary>
    /// The trigger that was rate limited.
    /// </summary>
    public object? Trigger { get; }

    /// <summary>
    /// Number of tokens that were available.
    /// </summary>
    public int AvailableTokens { get; }

    /// <summary>
    /// Number of tokens that were required.
    /// </summary>
    public int RequiredTokens { get; }

    /// <summary>
    /// Estimated time until enough tokens are available.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public RateLimitExceededException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with detailed rate limit info.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="trigger">The trigger that was rate limited.</param>
    /// <param name="availableTokens">Number of tokens that were available.</param>
    /// <param name="requiredTokens">Number of tokens that were required.</param>
    /// <param name="retryAfter">Estimated time until enough tokens are available.</param>
    public RateLimitExceededException(
        string message,
        object? trigger,
        int availableTokens,
        int requiredTokens,
        TimeSpan? retryAfter = null)
        : base(message)
    {
        Trigger = trigger;
        AvailableTokens = availableTokens;
        RequiredTokens = requiredTokens;
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RateLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
