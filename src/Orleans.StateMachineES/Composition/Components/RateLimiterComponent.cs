using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Stateless;

namespace Orleans.StateMachineES.Composition.Components;

/// <summary>
/// Rate limiter component that controls the rate of state machine transitions using a token bucket algorithm.
/// Protects against burst traffic and ensures fair resource allocation.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public class RateLimiterComponent<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly ILogger? _logger;
    private readonly RateLimiterOptions _options;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private int _availableTokens;
    private DateTime _lastRefillTime;
    private long _totalAllowed;
    private long _totalRejected;
    private DateTime? _lastAcquireTime;

    /// <summary>
    /// Timeout for acquiring semaphore locks to prevent deadlocks (30 seconds).
    /// </summary>
    private static readonly TimeSpan SemaphoreTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of the RateLimiterComponent class.
    /// </summary>
    /// <param name="options">Configuration options for the rate limiter.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public RateLimiterComponent(RateLimiterOptions options, ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        // Initialize with full burst capacity
        _availableTokens = options.BurstCapacity;
        _lastRefillTime = DateTime.UtcNow;

        ValidateOptions();
    }

    /// <summary>
    /// Gets the current configuration options.
    /// </summary>
    public RateLimiterOptions Options => _options;

    /// <summary>
    /// Gets the current number of available tokens.
    /// </summary>
    public int AvailableTokens => _availableTokens;

    /// <summary>
    /// Gets the total number of requests that were allowed.
    /// </summary>
    public long TotalAllowed => Interlocked.Read(ref _totalAllowed);

    /// <summary>
    /// Gets the total number of requests that were rejected.
    /// </summary>
    public long TotalRejected => Interlocked.Read(ref _totalRejected);

    /// <summary>
    /// Attempts to acquire tokens before firing a trigger.
    /// </summary>
    /// <param name="trigger">The trigger being fired.</param>
    /// <param name="stateMachine">The state machine instance.</param>
    /// <returns>True if tokens were acquired and trigger can proceed; false otherwise.</returns>
    public async Task<bool> TryAcquireAsync(TTrigger trigger, StateMachine<TState, TTrigger> stateMachine)
    {
        // Check if this trigger is monitored
        if (!IsTriggerMonitored(trigger))
        {
            return true;
        }

        if (!await _tokenLock.WaitAsync(SemaphoreTimeout))
        {
            throw new TimeoutException(
                $"Failed to acquire rate limiter lock within {SemaphoreTimeout.TotalSeconds}s - potential deadlock detected");
        }

        try
        {
            // Refill tokens based on elapsed time
            RefillTokens();

            var requiredTokens = _options.TokensPerOperation;

            // Check if we have enough tokens
            if (_availableTokens >= requiredTokens)
            {
                _availableTokens -= requiredTokens;
                _lastAcquireTime = DateTime.UtcNow;
                Interlocked.Increment(ref _totalAllowed);

                _logger?.LogDebug(
                    "Rate limiter: Acquired {Required} token(s) for trigger {Trigger}. Remaining: {Remaining}",
                    requiredTokens, trigger, _availableTokens);

                // Invoke callback
                try
                {
                    _options.OnTokensAcquired?.Invoke(trigger!, _availableTokens);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception in OnTokensAcquired callback");
                }

                return true;
            }

            // Not enough tokens - handle based on options
            return await HandleInsufficientTokensAsync(trigger, requiredTokens);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Handles the case when there are insufficient tokens.
    /// </summary>
    private async Task<bool> HandleInsufficientTokensAsync(TTrigger trigger, int requiredTokens)
    {
        // Calculate retry-after time
        var tokensNeeded = requiredTokens - _availableTokens;
        var retryAfter = CalculateRetryAfter(tokensNeeded);

        Interlocked.Increment(ref _totalRejected);

        _logger?.LogWarning(
            "Rate limiter: Rejected trigger {Trigger}. Available: {Available}, Required: {Required}, RetryAfter: {RetryAfter}ms",
            trigger, _availableTokens, requiredTokens, retryAfter?.TotalMilliseconds);

        // Invoke callback
        try
        {
            _options.OnRateLimitExceeded?.Invoke(trigger!, _availableTokens, requiredTokens);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception in OnRateLimitExceeded callback");
        }

        // If configured to wait, try to wait for tokens
        if (_options.MaxWaitTime > TimeSpan.Zero && retryAfter.HasValue && retryAfter.Value <= _options.MaxWaitTime)
        {
            _logger?.LogDebug("Rate limiter: Waiting {WaitTime}ms for tokens", retryAfter.Value.TotalMilliseconds);

            // Release lock while waiting
            _tokenLock.Release();

            await Task.Delay(retryAfter.Value);

            // Re-acquire lock and retry
            if (!await _tokenLock.WaitAsync(SemaphoreTimeout))
            {
                throw new TimeoutException("Failed to re-acquire rate limiter lock after waiting");
            }

            RefillTokens();

            if (_availableTokens >= requiredTokens)
            {
                _availableTokens -= requiredTokens;
                _lastAcquireTime = DateTime.UtcNow;
                Interlocked.Increment(ref _totalAllowed);
                Interlocked.Decrement(ref _totalRejected); // Undo the rejection count

                _logger?.LogDebug(
                    "Rate limiter: Acquired {Required} token(s) after waiting. Remaining: {Remaining}",
                    requiredTokens, _availableTokens);

                return true;
            }
        }

        // Throw or return false based on configuration
        if (_options.ThrowWhenExceeded)
        {
            throw new RateLimitExceededException(
                $"Rate limit exceeded for trigger '{trigger}'. Available: {_availableTokens}, Required: {requiredTokens}",
                trigger,
                _availableTokens,
                requiredTokens,
                retryAfter);
        }

        return false;
    }

    /// <summary>
    /// Refills tokens based on elapsed time since last refill.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRefillTime;

        if (elapsed < TimeSpan.Zero)
        {
            // Clock skew protection
            _lastRefillTime = now;
            return;
        }

        if (_options.UseSlidingWindow)
        {
            // Sliding window: refill proportionally based on elapsed time
            var tokensToAdd = (int)(elapsed.TotalSeconds * _options.TokensPerSecond);

            if (tokensToAdd > 0)
            {
                var previousTokens = _availableTokens;
                _availableTokens = Math.Min(_availableTokens + tokensToAdd, _options.BurstCapacity);
                _lastRefillTime = now;

                var actualAdded = _availableTokens - previousTokens;
                if (actualAdded > 0)
                {
                    _logger?.LogTrace(
                        "Rate limiter: Refilled {Added} tokens. Total: {Total}",
                        actualAdded, _availableTokens);

                    try
                    {
                        _options.OnTokensRefilled?.Invoke(actualAdded, _availableTokens);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Exception in OnTokensRefilled callback");
                    }
                }
            }
        }
        else
        {
            // Fixed window: refill fully at each interval boundary
            var intervalsPassed = (int)(elapsed.TotalMilliseconds / _options.RefillInterval.TotalMilliseconds);

            if (intervalsPassed > 0)
            {
                var tokensToAdd = intervalsPassed * _options.TokensPerInterval;
                var previousTokens = _availableTokens;
                _availableTokens = Math.Min(_availableTokens + tokensToAdd, _options.BurstCapacity);
                _lastRefillTime = now;

                var actualAdded = _availableTokens - previousTokens;
                if (actualAdded > 0)
                {
                    _logger?.LogTrace(
                        "Rate limiter: Refilled {Added} tokens (fixed window). Total: {Total}",
                        actualAdded, _availableTokens);

                    try
                    {
                        _options.OnTokensRefilled?.Invoke(actualAdded, _availableTokens);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Exception in OnTokensRefilled callback");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Calculates the estimated time until enough tokens are available.
    /// </summary>
    private TimeSpan? CalculateRetryAfter(int tokensNeeded)
    {
        if (_options.TokensPerSecond <= 0)
        {
            return null;
        }

        var secondsNeeded = tokensNeeded / _options.TokensPerSecond;
        return TimeSpan.FromSeconds(Math.Ceiling(secondsNeeded));
    }

    /// <summary>
    /// Checks if a trigger is being monitored by this rate limiter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsTriggerMonitored(TTrigger trigger)
    {
        if (_options.MonitoredTriggers == null || _options.MonitoredTriggers.Length == 0)
        {
            return true; // Monitor all triggers
        }

        foreach (var monitored in _options.MonitoredTriggers)
        {
            if (monitored is TTrigger mt && EqualityComparer<TTrigger>.Default.Equals(mt, trigger))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates the options configuration.
    /// </summary>
    private void ValidateOptions()
    {
        if (_options.TokensPerInterval <= 0)
        {
            throw new ArgumentException("TokensPerInterval must be greater than 0", nameof(_options));
        }

        if (_options.BurstCapacity < _options.TokensPerInterval)
        {
            throw new ArgumentException(
                "BurstCapacity must be greater than or equal to TokensPerInterval",
                nameof(_options));
        }

        if (_options.RefillInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("RefillInterval must be greater than zero", nameof(_options));
        }

        if (_options.TokensPerOperation <= 0)
        {
            throw new ArgumentException("TokensPerOperation must be greater than 0", nameof(_options));
        }
    }

    /// <summary>
    /// Manually resets the rate limiter to full capacity.
    /// </summary>
    public void Reset()
    {
        _tokenLock.Wait();
        try
        {
            _availableTokens = _options.BurstCapacity;
            _lastRefillTime = DateTime.UtcNow;
            _logger?.LogInformation("Rate limiter manually reset to full capacity ({Capacity} tokens)", _options.BurstCapacity);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Gets current rate limiter statistics.
    /// </summary>
    public RateLimiterStats GetStatistics()
    {
        return new RateLimiterStats
        {
            AvailableTokens = _availableTokens,
            BurstCapacity = _options.BurstCapacity,
            TokensPerInterval = _options.TokensPerInterval,
            RefillInterval = _options.RefillInterval,
            TotalAllowed = Interlocked.Read(ref _totalAllowed),
            TotalRejected = Interlocked.Read(ref _totalRejected),
            LastAcquireTime = _lastAcquireTime,
            LastRefillTime = _lastRefillTime,
            EffectiveRate = _options.TokensPerSecond
        };
    }

    /// <summary>
    /// Validates a transition (always returns true - rate limiting is done via TryAcquireAsync).
    /// </summary>
    public Task<bool> ValidateTransitionAsync(TState fromState, TState toState, TTrigger trigger)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Called after a transition completes (no-op for rate limiter).
    /// </summary>
    public Task OnTransitionAsync(TState fromState, TState toState, TTrigger trigger)
    {
        return Task.CompletedTask;
    }
}
