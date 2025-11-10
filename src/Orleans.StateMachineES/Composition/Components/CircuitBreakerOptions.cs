namespace Orleans.StateMachineES.Composition.Components;

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
