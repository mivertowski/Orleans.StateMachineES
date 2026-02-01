namespace Orleans.StateMachineES.Composition.Components;

/// <summary>
/// Statistics for a rate limiter component.
/// </summary>
public class RateLimiterStats
{
    /// <summary>
    /// Current number of available tokens.
    /// </summary>
    public int AvailableTokens { get; set; }

    /// <summary>
    /// Maximum burst capacity.
    /// </summary>
    public int BurstCapacity { get; set; }

    /// <summary>
    /// Configured tokens per interval.
    /// </summary>
    public int TokensPerInterval { get; set; }

    /// <summary>
    /// Token refill interval.
    /// </summary>
    public TimeSpan RefillInterval { get; set; }

    /// <summary>
    /// Total number of requests that were allowed.
    /// </summary>
    public long TotalAllowed { get; set; }

    /// <summary>
    /// Total number of requests that were rejected.
    /// </summary>
    public long TotalRejected { get; set; }

    /// <summary>
    /// When the last token was acquired.
    /// </summary>
    public DateTime? LastAcquireTime { get; set; }

    /// <summary>
    /// When tokens were last refilled.
    /// </summary>
    public DateTime LastRefillTime { get; set; }

    /// <summary>
    /// Current effective rate (tokens per second).
    /// </summary>
    public double EffectiveRate { get; set; }

    /// <summary>
    /// Utilization percentage (0-100).
    /// </summary>
    public double UtilizationPercentage => BurstCapacity > 0
        ? Math.Round((1 - (double)AvailableTokens / BurstCapacity) * 100, 2)
        : 0;

    /// <summary>
    /// Rejection rate percentage.
    /// </summary>
    public double RejectionRate => TotalAllowed + TotalRejected > 0
        ? Math.Round((double)TotalRejected / (TotalAllowed + TotalRejected) * 100, 2)
        : 0;
}
