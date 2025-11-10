namespace Orleans.StateMachineES.Composition.Components;

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
