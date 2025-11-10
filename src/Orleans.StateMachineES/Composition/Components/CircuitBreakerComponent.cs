using Microsoft.Extensions.Logging;
using Stateless;

namespace Orleans.StateMachineES.Composition.Components;

/// <summary>
/// Circuit breaker component that prevents cascading failures by temporarily blocking operations
/// when failure threshold is exceeded. Implements the Circuit Breaker pattern for state machines.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
/// <remarks>
/// Initializes a new instance of the CircuitBreakerComponent class.
/// </remarks>
/// <param name="options">Configuration options for the circuit breaker.</param>
/// <param name="logger">Optional logger for diagnostics.</param>
public class CircuitBreakerComponent<TState, TTrigger>(CircuitBreakerOptions options, ILogger? logger = null)
    where TState : notnull
    where TTrigger : notnull
{
    private readonly ILogger? _logger = logger;
    private readonly CircuitBreakerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private CircuitState _circuitState = CircuitState.Closed;
    private int _consecutiveFailures = 0;
    private DateTime _lastFailureTime;
    private DateTime? _circuitOpenedTime;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    /// <summary>
    /// Timeout for acquiring semaphore locks to prevent deadlocks (30 seconds).
    /// </summary>
    private static readonly TimeSpan SemaphoreTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The current state of the circuit breaker.
    /// </summary>
    public CircuitState State => _circuitState;

    /// <summary>
    /// Number of consecutive failures recorded.
    /// </summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>
    /// Number of consecutive successes recorded (used in HalfOpen state).
    /// </summary>
    private int _consecutiveSuccesses = 0;
    public int ConsecutiveSuccesses => _consecutiveSuccesses;

    /// <summary>
    /// When the circuit was last opened, if applicable.
    /// </summary>
    public DateTime? CircuitOpenedTime => _circuitOpenedTime;

    /// <summary>
    /// Applies circuit breaker logic before firing a trigger.
    /// </summary>
    public async Task<bool> BeforeFireAsync(TTrigger trigger, StateMachine<TState, TTrigger> stateMachine)
    {
        if (!await _stateLock.WaitAsync(SemaphoreTimeout))
        {
            throw new TimeoutException($"Failed to acquire circuit breaker lock within {SemaphoreTimeout.TotalSeconds}s - potential deadlock detected");
        }
        try
        {
            // Check if circuit should transition from Open to HalfOpen
            if (_circuitState == CircuitState.Open && _circuitOpenedTime.HasValue)
            {
                var elapsed = DateTime.UtcNow - _circuitOpenedTime.Value;
                if (elapsed >= _options.OpenDuration)
                {
                    _logger?.LogInformation("Circuit breaker entering Half-Open state after {Duration}ms",
                        elapsed.TotalMilliseconds);
                    _circuitState = CircuitState.HalfOpen;
                    _circuitOpenedTime = null;
                    _consecutiveSuccesses = 0;

                    // Invoke callback if configured (with exception handling to prevent callback failures from affecting circuit state)
                    try
                    {
                        _options.OnCircuitHalfOpened?.Invoke(_circuitState);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Exception in OnCircuitHalfOpened callback - circuit breaker state transition will proceed");
                    }
                }
            }

            // Block if circuit is open
            if (_circuitState == CircuitState.Open)
            {
                _logger?.LogWarning("Circuit breaker is OPEN - blocking trigger {Trigger}", trigger);

                if (_options.ThrowWhenOpen)
                {
                    throw new CircuitBreakerOpenException(
                        $"Circuit breaker is open. Trigger '{trigger}' blocked.");
                }

                return false; // Don't allow trigger to fire
            }

            // Allow trigger in Closed or HalfOpen states
            return true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Records a successful trigger execution.
    /// </summary>
    public async Task AfterFireSuccessAsync(TTrigger trigger)
    {
        // Check if this trigger is monitored
        if (_options.MonitoredTriggers != null && _options.MonitoredTriggers.Length > 0)
        {
            bool isMonitored = false;
            foreach (var monitored in _options.MonitoredTriggers)
            {
                if (monitored is TTrigger mt && EqualityComparer<TTrigger>.Default.Equals(mt, trigger))
                {
                    isMonitored = true;
                    break;
                }
            }
            if (!isMonitored)
            {
                return;
            }
        }

        if (!await _stateLock.WaitAsync(SemaphoreTimeout))
        {
            throw new TimeoutException($"Failed to acquire circuit breaker lock within {SemaphoreTimeout.TotalSeconds}s - potential deadlock detected");
        }
        try
        {
            await RecordSuccessAsync();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Records a failed trigger execution.
    /// </summary>
    public async Task AfterFireFailureAsync(TTrigger trigger, Exception exception)
    {
        // Check if this trigger is monitored
        if (_options.MonitoredTriggers != null && _options.MonitoredTriggers.Length > 0)
        {
            bool isMonitored = false;
            foreach (var monitored in _options.MonitoredTriggers)
            {
                if (monitored is TTrigger mt && EqualityComparer<TTrigger>.Default.Equals(mt, trigger))
                {
                    isMonitored = true;
                    break;
                }
            }
            if (!isMonitored)
            {
                return;
            }
        }

        if (!await _stateLock.WaitAsync(SemaphoreTimeout))
        {
            throw new TimeoutException($"Failed to acquire circuit breaker lock within {SemaphoreTimeout.TotalSeconds}s - potential deadlock detected");
        }
        try
        {
            await RecordFailureAsync(trigger, exception);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    private async Task RecordSuccessAsync()
    {
        if (_circuitState == CircuitState.HalfOpen)
        {
            _consecutiveSuccesses++;
            _consecutiveFailures = 0;

            _logger?.LogInformation(
                "Circuit breaker recorded success #{Count} in Half-Open state",
                _consecutiveSuccesses);

            // Check if we've reached the success threshold to close the circuit
            if (_consecutiveSuccesses >= _options.SuccessThreshold)
            {
                await CloseCircuitAsync();
            }
        }
        else if (_circuitState == CircuitState.Closed)
        {
            // Reset failure count on success
            _consecutiveFailures = 0;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    private async Task RecordFailureAsync(TTrigger trigger, Exception? exception)
    {
        _consecutiveFailures++;
        _lastFailureTime = DateTime.UtcNow;

        _logger?.LogWarning(exception,
            "Circuit breaker recorded failure #{Count} for trigger {Trigger}",
            _consecutiveFailures, trigger);

        // Check if we should open the circuit
        if (_circuitState == CircuitState.Closed &&
            _consecutiveFailures >= _options.FailureThreshold)
        {
            await OpenCircuitAsync();
        }
        else if (_circuitState == CircuitState.HalfOpen)
        {
            // Any failure in Half-Open immediately reopens the circuit
            await OpenCircuitAsync();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Opens the circuit breaker.
    /// </summary>
    private async Task OpenCircuitAsync()
    {
        _circuitState = CircuitState.Open;
        _circuitOpenedTime = DateTime.UtcNow;
        _consecutiveSuccesses = 0;

        _logger?.LogError(
            "Circuit breaker OPENED after {Count} consecutive failures. Will retry after {Duration}ms",
            _consecutiveFailures,
            _options.OpenDuration.TotalMilliseconds);

        // Invoke callback if configured (with exception handling to prevent callback failures from affecting circuit state)
        try
        {
            _options.OnCircuitOpened?.Invoke(_circuitState, _consecutiveFailures);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception in OnCircuitOpened callback - circuit breaker state transition will proceed");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Closes the circuit breaker.
    /// </summary>
    private async Task CloseCircuitAsync()
    {
        _circuitState = CircuitState.Closed;
        _consecutiveFailures = 0;
        _consecutiveSuccesses = 0;
        _circuitOpenedTime = null;

        _logger?.LogInformation("Circuit breaker CLOSED after successful recovery");

        // Invoke callback if configured (with exception handling to prevent callback failures from affecting circuit state)
        try
        {
            _options.OnCircuitClosed?.Invoke(_circuitState);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception in OnCircuitClosed callback - circuit breaker state transition will proceed");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Manually resets the circuit breaker to closed state.
    /// </summary>
    public void Reset()
    {
        _stateLock.Wait();
        try
        {
            _circuitState = CircuitState.Closed;
            _consecutiveFailures = 0;
            _consecutiveSuccesses = 0;
            _circuitOpenedTime = null;
            _logger?.LogInformation("Circuit breaker manually reset to Closed state");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Gets circuit breaker statistics.
    /// </summary>
    public CircuitBreakerStats GetStatistics()
    {
        return new CircuitBreakerStats
        {
            CircuitState = _circuitState,
            ConsecutiveFailures = _consecutiveFailures,
            LastFailureTime = _lastFailureTime,
            CircuitOpenedTime = _circuitOpenedTime,
            FailureThreshold = _options.FailureThreshold,
            OpenDuration = _options.OpenDuration
        };
    }

    /// <inheritdoc/>
    public Task<bool> ValidateTransitionAsync(TState fromState, TState toState, TTrigger trigger)
    {
        // Circuit breaker doesn't validate transitions, only manages failure states
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task OnTransitionAsync(TState fromState, TState toState, TTrigger trigger)
    {
        // No special handling needed on transition
        return Task.CompletedTask;
    }
}
