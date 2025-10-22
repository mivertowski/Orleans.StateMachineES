using Orleans.StateMachineES.Models;

namespace Orleans.StateMachineES.Visualization;

/// <summary>
/// Options for customizing state machine visualization output.
/// </summary>
public class VisualizationOptions
{
    /// <summary>
    /// Title to display on the generated visualization.
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Whether to include metadata such as generation timestamp.
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;
    
    /// <summary>
    /// Whether to highlight the current state in the visualization.
    /// </summary>
    public bool HighlightCurrentState { get; set; } = true;
    
    /// <summary>
    /// Whether to show trigger parameters in the visualization.
    /// </summary>
    public bool ShowTriggerParameters { get; set; } = false;
    
    /// <summary>
    /// Whether to include guard conditions in the visualization.
    /// </summary>
    public bool ShowGuardConditions { get; set; } = false;
    
    /// <summary>
    /// Whether to show entry/exit actions for states.
    /// </summary>
    public bool ShowStateActions { get; set; } = false;
    
    /// <summary>
    /// Color scheme to use for the visualization.
    /// </summary>
    public VisualizationColorScheme ColorScheme { get; set; } = VisualizationColorScheme.Default;
    
    /// <summary>
    /// Layout direction for the graph.
    /// </summary>
    public GraphLayout Layout { get; set; } = GraphLayout.TopToBottom;
}

/// <summary>
/// Available color schemes for visualization.
/// </summary>
public enum VisualizationColorScheme
{
    Default,
    Professional,
    HighContrast,
    Pastel,
    Monochrome
}

/// <summary>
/// Available graph layout directions.
/// </summary>
public enum GraphLayout
{
    TopToBottom,
    LeftToRight,
    BottomToTop,
    RightToLeft
}

/// <summary>
/// Supported export formats for state machine visualization.
/// </summary>
public enum ExportFormat
{
    Dot,
    Json,
    Xml,
    Mermaid,
    PlantUml
}

/// <summary>
/// Comprehensive analysis of a state machine's structure and properties.
/// </summary>
public class StateMachineAnalysis
{
    /// <summary>
    /// Type name of the state machine.
    /// </summary>
    public string StateMachineType { get; set; } = string.Empty;
    
    /// <summary>
    /// Type name of the states.
    /// </summary>
    public string StateType { get; set; } = string.Empty;
    
    /// <summary>
    /// Type name of the triggers.
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;
    
    /// <summary>
    /// When this analysis was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }
    
    /// <summary>
    /// Current state of the state machine at analysis time.
    /// </summary>
    public string CurrentState { get; set; } = string.Empty;
    
    /// <summary>
    /// Information about all states in the state machine.
    /// </summary>
    public List<StateInfo> States { get; set; } = [];
    
    /// <summary>
    /// Information about all triggers in the state machine.
    /// </summary>
    public List<TriggerInfo> Triggers { get; set; } = [];
    
    /// <summary>
    /// Complexity metrics for the state machine.
    /// </summary>
    public ComplexityMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Information about a specific state in the state machine.
/// </summary>
public class StateInfo
{
    /// <summary>
    /// Name of the state.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this is the initial state.
    /// </summary>
    public bool IsInitial { get; set; }
    
    /// <summary>
    /// Whether this is the current state.
    /// </summary>
    public bool IsCurrent { get; set; }
    
    /// <summary>
    /// Entry actions configured for this state.
    /// </summary>
    public List<string> EntryActions { get; set; } = [];
    
    /// <summary>
    /// Exit actions configured for this state.
    /// </summary>
    public List<string> ExitActions { get; set; } = [];
    
    /// <summary>
    /// Number of internal transitions (self-loops) for this state.
    /// </summary>
    public int InternalTransitions { get; set; }
    
    /// <summary>
    /// Substates if this is a hierarchical state machine.
    /// </summary>
    public List<string> Substates { get; set; } = [];
}

/// <summary>
/// Information about a specific trigger in the state machine.
/// </summary>
public class TriggerInfo
{
    /// <summary>
    /// Name of the trigger.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// How many times this trigger is used across all states.
    /// </summary>
    public int UsageCount { get; set; }
    
    /// <summary>
    /// States that can fire this trigger.
    /// </summary>
    public List<string> SourceStates { get; set; } = [];
    
    /// <summary>
    /// States that this trigger can transition to.
    /// </summary>
    public List<string> TargetStates { get; set; } = [];
    
    /// <summary>
    /// Whether this trigger has guard conditions.
    /// </summary>
    public bool HasGuards { get; set; }
    
    /// <summary>
    /// Whether this trigger accepts parameters.
    /// </summary>
    public bool HasParameters { get; set; }
}

/// <summary>
/// Complexity metrics for analyzing state machine design quality.
/// </summary>
public class ComplexityMetrics
{
    /// <summary>
    /// Total number of states.
    /// </summary>
    public int StateCount { get; set; }
    
    /// <summary>
    /// Total number of unique triggers.
    /// </summary>
    public int TriggerCount { get; set; }
    
    /// <summary>
    /// Total number of state transitions.
    /// </summary>
    public int TransitionCount { get; set; }
    
    /// <summary>
    /// Cyclomatic complexity measure.
    /// </summary>
    public int CyclomaticComplexity { get; set; }
    
    /// <summary>
    /// Maximum depth of state hierarchy.
    /// </summary>
    public int MaxDepth { get; set; }
    
    /// <summary>
    /// Connectivity index (0-1, higher means more interconnected).
    /// </summary>
    public double ConnectivityIndex { get; set; }
    
    /// <summary>
    /// Complexity assessment based on metrics.
    /// </summary>
    public ComplexityLevel ComplexityLevel => CalculateComplexityLevel();
    
    private ComplexityLevel CalculateComplexityLevel()
    {
        var score = 0;
        
        if (StateCount > 20) score += 2;
        else if (StateCount > 10) score += 1;
        
        if (TriggerCount > 15) score += 2;
        else if (TriggerCount > 8) score += 1;
        
        if (CyclomaticComplexity > 20) score += 3;
        else if (CyclomaticComplexity > 10) score += 2;
        else if (CyclomaticComplexity > 5) score += 1;
        
        if (MaxDepth > 4) score += 2;
        else if (MaxDepth > 2) score += 1;
        
        return score switch
        {
            <= 2 => ComplexityLevel.Low,
            <= 5 => ComplexityLevel.Medium,
            <= 8 => ComplexityLevel.High,
            _ => ComplexityLevel.VeryHigh
        };
    }
}

/// <summary>
/// Complexity levels for state machine design assessment.
/// </summary>
public enum ComplexityLevel
{
    Low,
    Medium,
    High,
    VeryHigh
}

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

/// <summary>
/// Configuration for batch visualization operations.
/// </summary>
public class BatchVisualizationOptions
{
    /// <summary>
    /// Formats to export to.
    /// </summary>
    public List<ExportFormat> Formats { get; set; } = [];
    
    /// <summary>
    /// Base output directory for batch exports.
    /// </summary>
    public string? OutputDirectory { get; set; }
    
    /// <summary>
    /// Filename prefix for generated files.
    /// </summary>
    public string FilePrefix { get; set; } = "StateMachine";
    
    /// <summary>
    /// Whether to include timestamp in filenames.
    /// </summary>
    public bool IncludeTimestampInFilename { get; set; } = true;
    
    /// <summary>
    /// Visualization options to apply to all exports.
    /// </summary>
    public VisualizationOptions VisualizationOptions { get; set; } = new();
}