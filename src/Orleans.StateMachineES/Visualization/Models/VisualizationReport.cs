using Orleans.StateMachineES.Models;

namespace Orleans.StateMachineES.Visualization;

/// <summary>
/// Comprehensive report including visualization and runtime information.
/// </summary>
public class VisualizationReport
{
    /// <summary>
    /// Type name of the grain generating this report.
    /// </summary>
    public string GrainType { get; set; } = string.Empty;

    /// <summary>
    /// When this report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Whether runtime information was included.
    /// </summary>
    public bool IncludesRuntimeInfo { get; set; }

    /// <summary>
    /// Whether the report generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if report generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// State machine configuration information.
    /// </summary>
    public OrleansStateMachineInfo? StateMachineInfo { get; set; }

    /// <summary>
    /// Current state at the time of report generation.
    /// </summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>
    /// Currently permitted triggers.
    /// </summary>
    public List<string> PermittedTriggers { get; set; } = [];

    /// <summary>
    /// State transition history (if available and requested).
    /// </summary>
    public List<string> StateHistory { get; set; } = [];

    /// <summary>
    /// Additional runtime metrics.
    /// </summary>
    public Dictionary<string, object> RuntimeMetrics { get; set; } = [];
}
