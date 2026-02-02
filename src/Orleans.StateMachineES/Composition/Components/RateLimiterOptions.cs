namespace Orleans.StateMachineES.Composition.Components;

/// <summary>
/// Configuration options for the rate limiter component.
/// Uses a token bucket algorithm for rate limiting.
/// </summary>
public class RateLimiterOptions
{
    /// <summary>
    /// Maximum number of tokens (transitions) per refill interval.
    /// This is the sustained rate limit.
    /// Default: 100
    /// </summary>
    public int TokensPerInterval { get; set; } = 100;

    /// <summary>
    /// Maximum burst capacity (tokens that can accumulate).
    /// Allows short bursts above the sustained rate.
    /// Default: 150
    /// </summary>
    public int BurstCapacity { get; set; } = 150;

    /// <summary>
    /// Interval at which tokens are refilled.
    /// Default: 1 second
    /// </summary>
    public TimeSpan RefillInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to throw an exception when rate limit is exceeded or return false silently.
    /// Default: false
    /// </summary>
    public bool ThrowWhenExceeded { get; set; } = false;

    /// <summary>
    /// Optional list of triggers to rate limit. If null or empty, all triggers are limited.
    /// </summary>
    public object[]? MonitoredTriggers { get; set; }

    /// <summary>
    /// Whether to enable sliding window rate limiting (smoother distribution).
    /// When false, uses fixed window (simpler but can have edge bursts).
    /// Default: true
    /// </summary>
    public bool UseSlidingWindow { get; set; } = true;

    /// <summary>
    /// Number of tokens to consume per operation.
    /// Default: 1
    /// </summary>
    public int TokensPerOperation { get; set; } = 1;

    /// <summary>
    /// Maximum time to wait for a token to become available.
    /// If TimeSpan.Zero, returns immediately without waiting.
    /// Default: TimeSpan.Zero (no waiting)
    /// </summary>
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Callback invoked when rate limit is exceeded.
    /// Parameters: (trigger, available tokens, required tokens)
    /// </summary>
    public Action<object, int, int>? OnRateLimitExceeded { get; set; }

    /// <summary>
    /// Callback invoked when tokens are successfully acquired.
    /// Parameters: (trigger, remaining tokens)
    /// </summary>
    public Action<object, int>? OnTokensAcquired { get; set; }

    /// <summary>
    /// Callback invoked when tokens are refilled.
    /// Parameters: (tokens added, total tokens)
    /// </summary>
    public Action<int, int>? OnTokensRefilled { get; set; }

    /// <summary>
    /// Gets the effective tokens per second rate.
    /// </summary>
    public double TokensPerSecond => TokensPerInterval / RefillInterval.TotalSeconds;
}
