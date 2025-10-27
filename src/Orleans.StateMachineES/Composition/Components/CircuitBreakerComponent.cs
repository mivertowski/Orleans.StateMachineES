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
        await _stateLock.WaitAsync();
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

                    // Invoke callback if configured
                    _options.OnCircuitHalfOpened?.Invoke(_circuitState);
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

        await _stateLock.WaitAsync();
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

        await _stateLock.WaitAsync();
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

        // Invoke callback if configured
        _options.OnCircuitOpened?.Invoke(_circuitState, _consecutiveFailures);

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

        // Invoke callback if configured
        _options.OnCircuitClosed?.Invoke(_circuitState);

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

/// <summary>
/// Configuration options for the circuit breaker component.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Number of consecutive failures before opening the circuit.
    /// Default: 5
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Number of consecutive successes in HalfOpen state before closing the circuit.
    /// Default: 2
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;

    /// <summary>
    /// Duration to keep the circuit open before attempting Half-Open state.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to throw an exception when circuit is open or silently block.
    /// Default: true
    /// </summary>
    public bool ThrowWhenOpen { get; set; } = true;

    /// <summary>
    /// Optional list of triggers to monitor. If null or empty, all triggers are monitored.
    /// </summary>
    public object[]? MonitoredTriggers { get; set; }

    /// <summary>
    /// Callback invoked when circuit opens.
    /// Parameters: (CircuitState state, int failureCount)
    /// </summary>
    public Action<CircuitState, int>? OnCircuitOpened { get; set; }

    /// <summary>
    /// Callback invoked when circuit closes after being open.
    /// Parameters: (CircuitState state)
    /// </summary>
    public Action<CircuitState>? OnCircuitClosed { get; set; }

    /// <summary>
    /// Callback invoked when circuit enters half-open state.
    /// Parameters: (CircuitState state)
    /// </summary>
    public Action<CircuitState>? OnCircuitHalfOpened { get; set; }
}

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed - operations proceed normally.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open - operations are blocked.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open - testing if system has recovered.
    /// </summary>
    HalfOpen
}

/// <summary>
/// Statistics for a circuit breaker.
/// </summary>
public class CircuitBreakerStats
{
    /// <summary>
    /// Current state of the circuit.
    /// </summary>
    public CircuitState CircuitState { get; set; }

    /// <summary>
    /// Number of consecutive failures recorded.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// When the last failure occurred.
    /// </summary>
    public DateTime LastFailureTime { get; set; }

    /// <summary>
    /// When the circuit was opened, if applicable.
    /// </summary>
    public DateTime? CircuitOpenedTime { get; set; }

    /// <summary>
    /// Configured failure threshold.
    /// </summary>
    public int FailureThreshold { get; set; }

    /// <summary>
    /// Configured open duration.
    /// </summary>
    public TimeSpan OpenDuration { get; set; }
}

/// <summary>
/// Exception thrown when circuit breaker is open and blocks an operation.
/// </summary>
public class CircuitBreakerOpenException : InvalidOperationException
{
    public CircuitBreakerOpenException(string message) : base(message)
    {
    }

    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
